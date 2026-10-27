import type {
  Action,
  ApiErrorBody,
  Automation,
  Condition,
  Device,
  DeviceCommand,
  DeviceReport,
  Discovery,
  Pairing,
  Presence,
  RunOutcome,
  SceneActivation,
  Simulation,
  TraceStep,
  TriggerField,
} from '../types';
import { createFixtures, iso, light, type MockData } from './fixtures';

/**
 * In-browser fake of the Homebase API, used when VITE_API_MODE is not "live".
 * It implements every operation in contracts/openapi.yaml (enforced by
 * mock-fetch.test.ts).
 *
 * MOCK ONLY: the rules below are a simplified mirror of the server so the UI is
 * explorable offline. Not modelled: sunrise/sunset ("dark" is simply 18:00–07:00),
 * forMinutes countdowns firing on their own, automations reacting to anything but
 * phone reports, the loop guard and real drivers. The server is authoritative;
 * never copy logic from here into application code.
 */
const LATENCY_MS = import.meta.env.MODE === 'test' ? 0 : 200;

let db: MockData = createFixtures();

/** Restores pristine fixtures. Called after every test in src/test/setup.ts. */
export function resetMockData(): void {
  db = createFixtures();
}

interface Context {
  params: Record<string, string>;
  url: URL;
  request: Request;
}

type Handler = (ctx: Context) => Response | Promise<Response>;

const nowIso = () => iso(Date.now());

const json = (body: unknown, status = 200) =>
  new Response(JSON.stringify(body), { status, headers: { 'Content-Type': 'application/json' } });

const fail = (status: number, code: string, message: string) =>
  json({ error: { code, message } } satisfies ApiErrorBody, status);

const found = <T>(value: T | undefined, what: string): Response =>
  value === undefined ? fail(404, 'not_found', `${what} not found`) : json(value);

async function readBody(request: Request): Promise<Record<string, unknown>> {
  try {
    const body: unknown = await request.json();
    return body !== null && typeof body === 'object' && !Array.isArray(body) ? (body as Record<string, unknown>) : {};
  } catch {
    return {};
  }
}

const findDevice = (id: string | null | undefined) => db.devices.find((d) => d.id === id);

function saveDevice(device: Device): Device {
  db.devices = db.devices.map((d) => (d.id === device.id ? device : d));
  return device;
}

function saveAutomation(automation: Automation): Automation {
  db.automations = db.automations.map((a) => (a.id === automation.id ? automation : a));
  return automation;
}

/** Ordered by room sort order (no room last), then name — as the contract says. */
function sortedDevices(): Device[] {
  const roomOrder = (roomId: string | null) => (roomId === null ? db.rooms.length : db.rooms.findIndex((r) => r.id === roomId));
  return [...db.devices].sort((a, b) => roomOrder(a.roomId) - roomOrder(b.roomId) || a.name.localeCompare(b.name));
}

const clock = (date: Date) => `${String(date.getHours()).padStart(2, '0')}:${String(date.getMinutes()).padStart(2, '0')}`;

function minutesOfDay(hhmm: string): number {
  const [h = 0, m = 0] = hhmm.split(':').map(Number);
  return h * 60 + m;
}

/** Without a location the server treats 18:00–07:00 as dark; the mock always does. */
const isDark = (date: Date) => date.getHours() >= 18 || date.getHours() < 7;

function presence(): Presence {
  return {
    home: findDevice('phone')?.state.home ?? null,
    phoneDeviceId: 'phone',
    tvDeviceId: 'tv',
    sun: { dark: isDark(new Date()), ...db.sun },
    reportToken: db.reportToken,
  };
}

const isInt = (value: unknown, min: number, max: number) =>
  typeof value === 'number' && Number.isInteger(value) && value >= min && value <= max;

/** Why a command body is invalid for a light, or null. */
function commandProblem(body: Record<string, unknown>): string | null {
  const unsupported = Object.keys(body).filter((k) => !['on', 'brightness', 'colorTempK'].includes(k));
  if (unsupported.length > 0) return `Lights don't support: ${unsupported.join(', ')}.`;
  if (Object.keys(body).length === 0) return 'Send at least one of on, brightness or colorTempK.';
  if (body.on !== undefined && typeof body.on !== 'boolean') return 'on must be true or false.';
  if (body.brightness !== undefined && !isInt(body.brightness, 1, 100)) return 'brightness must be a whole number from 1 to 100.';
  if (body.colorTempK !== undefined && !isInt(body.colorTempK, 2000, 9000)) return 'colorTempK must be a whole number from 2000 to 9000.';
  return null;
}

/** The API error code that stops a device taking a light command, or null. */
function lightProblem(device: Device): string | null {
  if (device.type !== 'light') return 'read_only_device';
  if (!device.online) return 'device_offline';
  return null;
}

function applyCommand(device: Device, command: DeviceCommand): Device {
  const state = { ...device.state };
  if (command.brightness !== undefined) {
    state.brightness = command.brightness;
    state.on = true;
  }
  if (command.on !== undefined) state.on = command.on;
  if (command.colorTempK !== undefined) state.colorTempK = command.colorTempK;
  return saveDevice({ ...device, state, lastChangedAt: nowIso() });
}

function commandOf(action: Action): DeviceCommand {
  const command: DeviceCommand = {};
  if (action.on != null) command.on = action.on;
  if (action.brightness != null) command.brightness = action.brightness;
  if (action.colorTempK != null) command.colorTempK = action.colorTempK;
  return command;
}

/** Runs one scene/automation action; returns an error code or null. One failing light never stops the others. */
function runAction(action: Action, depth = 0): string | null {
  if (action.kind === 'scene') {
    const scene = db.scenes.find((s) => s.id === action.sceneId);
    if (!scene) return 'not_found';
    if (depth > 2) return 'internal_error';
    return scene.actions.map((a) => runAction(a, depth + 1)).find((e) => e !== null) ?? null;
  }
  const targets = action.kind === 'room' ? db.devices.filter((d) => d.type === 'light' && d.roomId === action.roomId) : [findDevice(action.deviceId)];
  let error: string | null = null;
  for (const device of targets) {
    if (!device) {
      error = 'not_found';
      continue;
    }
    const problem = lightProblem(device);
    if (problem) error = problem;
    else applyCommand(device, commandOf(action));
  }
  return error;
}

function checkCondition(condition: Condition, now: Date): TraceStep {
  const label = `Condition: ${condition.summary}`;
  switch (condition.kind) {
    case 'time-window': {
      const after = minutesOfDay(condition.after ?? '00:00');
      const before = minutesOfDay(condition.before ?? '00:00');
      const minutes = now.getHours() * 60 + now.getMinutes();
      const passed = after <= before ? minutes >= after && minutes < before : minutes >= after || minutes < before;
      return { label, passed, detail: `Now is ${clock(now)} household time` };
    }
    case 'dark':
      return { label, passed: isDark(now) === condition.equals, detail: `Now is ${clock(now)}; no location set, so dark means 18:00–07:00` };
    case 'device': {
      const device = findDevice(condition.deviceId);
      const value = condition.field ? device?.state[condition.field] : undefined;
      return { label, passed: value === condition.equals, detail: `${device?.name ?? condition.deviceId}: ${condition.field} is ${value ?? 'unknown'}` };
    }
  }
}

function recordRun(automation: Automation, outcome: RunOutcome, trace: TraceStep[], actionsExecuted: number): void {
  const firedAt = nowIso();
  const run = { id: `run_${Date.now()}_${db.runs.length}`, automationId: automation.id, automationName: automation.name, firedAt, outcome, trace, actionsExecuted };
  db.runs = [run, ...db.runs];
  saveAutomation({ ...automation, lastRunAt: firedAt, lastOutcome: outcome, waitingUntil: null });
}

function runAutomation(automation: Automation, triggerDetail: string): void {
  const now = new Date();
  const trace: TraceStep[] = [
    { label: `Trigger: ${automation.trigger.summary}`, passed: true, detail: triggerDetail },
    ...automation.conditions.map((c) => checkCondition(c, now)),
  ];
  if (!trace.every((t) => t.passed)) return recordRun(automation, 'skipped', trace, 0);
  let executed = 0;
  for (const action of automation.actions) {
    const error = runAction(action);
    if (error) trace.push({ label: `Action: ${action.summary}`, passed: false, detail: `Not applied: ${error}` });
    else executed += 1;
  }
  recordRun(automation, executed === automation.actions.length ? 'executed' : 'failed', trace, executed);
}

/** Simplified edge trigger: a field changed into `equals` → run now, or start waiting for forMinutes. */
function onDeviceChanged(device: Device, field: TriggerField, from: boolean | null | undefined, to: boolean): void {
  for (const automation of db.automations) {
    const { trigger } = automation;
    if (!automation.enabled || trigger.kind !== 'device' || trigger.deviceId !== device.id || trigger.field !== field) continue;
    if (to === trigger.equals) {
      if (trigger.forMinutes) saveAutomation({ ...automation, waitingUntil: iso(Date.now() + trigger.forMinutes * 60_000) });
      else runAutomation(automation, `${field} changed ${String(from)} → ${to}`);
    } else if (automation.waitingUntil) {
      const detail = `${field} changed back to ${to} before ${trigger.forMinutes} min were up`;
      recordRun(automation, 'cancelled', [{ label: `Trigger: ${trigger.summary}`, passed: false, detail }], 0);
    }
  }
}

const REPORT_FIELDS = ['home', 'charging', 'asleep'] as const;

function reportProblem(body: Record<string, unknown>): string | null {
  const keys = Object.keys(body);
  if (keys.length === 0) return 'Send at least one of home, charging or asleep.';
  const unsupported = keys.filter((k) => !(REPORT_FIELDS as readonly string[]).includes(k));
  if (unsupported.length > 0) return `A phone doesn't report: ${unsupported.join(', ')}.`;
  const notBoolean = keys.filter((k) => typeof body[k] !== 'boolean');
  if (notBoolean.length > 0) return `${notBoolean.join(', ')} must be true or false.`;
  return null;
}

function simulate(automationId: string): Simulation | undefined {
  const automation = db.automations.find((a) => a.id === automationId);
  if (!automation) return undefined;
  const now = new Date();
  const { trigger } = automation;
  const trace: TraceStep[] = [
    {
      label: `Trigger: ${trigger.summary}`,
      passed: true,
      detail: trigger.forMinutes ? `Simulated as if it fired now and held for ${trigger.forMinutes} min` : 'Simulated as if it fired now',
    },
    ...automation.conditions.map((c) => checkCondition(c, now)),
  ];
  const wouldRun = trace.every((t) => t.passed);
  return { automationId, wouldRun, evaluatedAt: iso(now.getTime()), trace, plannedActions: wouldRun ? automation.actions : [] };
}

const PAIRING_CODE = /^[0-9A-Fa-f]{6}$/;

function findTv(deviceId: string | undefined): Device | Response {
  const device = findDevice(deviceId);
  if (!device) return fail(404, 'not_found', `Device ${deviceId} not found`);
  if (device.driver !== 'android-tv') return fail(422, 'not_pairable', `${device.name} can't be paired.`);
  return device;
}

// Literal routes must come before parameterised siblings (/discover before /{deviceId}, /runs before /{automationId}).
export const mockRoutes: Array<{ method: string; path: string; handler: Handler }> = [
  { method: 'GET', path: '/api/health', handler: () => json({ status: 'ok', database: 'ok' }) },
  { method: 'GET', path: '/api/presence', handler: () => json(presence()) },
  { method: 'GET', path: '/api/rooms', handler: () => json(db.rooms) },
  {
    method: 'GET',
    path: '/api/devices',
    handler: ({ url }) => {
      const roomId = url.searchParams.get('roomId');
      return json(sortedDevices().filter((d) => !roomId || d.roomId === roomId));
    },
  },
  {
    method: 'POST',
    path: '/api/devices/discover',
    handler: () => {
      if (db.discovered) {
        return json({ added: [], updated: [], notes: ['No new lights found on the local network or in the Govee cloud.'] } satisfies Discovery);
      }
      db.discovered = true;
      const deskLamp = light('light-desk-lamp', 'Desk lamp', null, 'govee-lan', { on: false, brightness: 100, colorTempK: 4000 }, { lastChangedAt: nowIso() });
      db.devices = [...db.devices, deskLamp];
      return json({
        added: [deskLamp],
        updated: [],
        notes: ['Found 1 new light on the local network. It is not in a room yet.', 'The Govee cloud has no lights Homebase does not already know.'],
      } satisfies Discovery);
    },
  },
  { method: 'GET', path: '/api/devices/{deviceId}', handler: ({ params }) => found(findDevice(params.deviceId), `Device ${params.deviceId}`) },
  {
    method: 'PUT',
    path: '/api/devices/{deviceId}',
    handler: async ({ params, request }) => {
      const device = findDevice(params.deviceId);
      if (!device) return fail(404, 'not_found', `Device ${params.deviceId} not found`);
      const { name, roomId } = await readBody(request);
      if (typeof name !== 'string' || name.trim().length === 0 || name.trim().length > 100) {
        return fail(422, 'validation_failed', 'The name must be 1–100 characters.');
      }
      const room = roomId === null ? null : db.rooms.find((r) => r.id === roomId);
      if (room === undefined) return fail(422, 'validation_failed', `Room ${String(roomId)} does not exist.`);
      return json(saveDevice({ ...device, name: name.trim(), roomId: room?.id ?? null }));
    },
  },
  {
    method: 'POST',
    path: '/api/devices/{deviceId}/commands',
    handler: async ({ params, request }) => {
      const device = findDevice(params.deviceId);
      if (!device) return fail(404, 'not_found', `Device ${params.deviceId} not found`);
      const problem = lightProblem(device);
      if (problem === 'read_only_device') return fail(422, problem, `${device.name} is read-only here.`);
      if (problem === 'device_offline') return fail(422, problem, `${device.name} is offline.`);
      const body = await readBody(request);
      const invalid = commandProblem(body);
      if (invalid) return fail(422, 'validation_failed', invalid);
      return json(applyCommand(device, body as DeviceCommand));
    },
  },
  {
    method: 'POST',
    path: '/api/devices/{deviceId}/report',
    handler: async ({ params, request }) => {
      if (request.headers.get('Authorization') !== `Bearer ${db.reportToken}`) return fail(401, 'unauthorized', 'Missing or wrong report token.');
      const device = findDevice(params.deviceId);
      if (!device) return fail(404, 'not_found', `Device ${params.deviceId} not found`);
      if (device.driver !== 'push') return fail(422, 'not_reportable', `${device.name} does not report its state.`);
      const body = await readBody(request);
      const invalid = reportProblem(body);
      if (invalid) return fail(422, 'validation_failed', invalid);
      const report = body as DeviceReport;
      const changed = REPORT_FIELDS.filter((f) => report[f] !== undefined && report[f] !== device.state[f]);
      const updated = saveDevice({ ...device, state: { ...device.state, ...report }, lastChangedAt: changed.length > 0 ? nowIso() : device.lastChangedAt });
      for (const field of changed) onDeviceChanged(updated, field, device.state[field], report[field] === true);
      return json(findDevice(device.id));
    },
  },
  {
    method: 'POST',
    path: '/api/devices/{deviceId}/pairing',
    handler: async ({ params, request }) => {
      const tv = findTv(params.deviceId);
      if (tv instanceof Response) return tv;
      const { address } = await readBody(request);
      if (address != null && (typeof address !== 'string' || address.trim() === '')) {
        return fail(422, 'validation_failed', 'The address must be an IP address or host name.');
      }
      return json({ deviceId: tv.id, message: `${tv.name} should now show a 6-character code. Enter it to finish pairing.` } satisfies Pairing);
    },
  },
  {
    method: 'POST',
    path: '/api/devices/{deviceId}/pairing/confirm',
    handler: async ({ params, request }) => {
      const tv = findTv(params.deviceId);
      if (tv instanceof Response) return tv;
      const { code } = await readBody(request);
      if (typeof code !== 'string' || !PAIRING_CODE.test(code)) {
        return fail(422, 'validation_failed', 'The code is 6 characters: digits 0–9 and letters A–F.');
      }
      if (code === '000000') return fail(422, 'pairing_failed', `${tv.name} did not accept that code. Start pairing again to get a new one.`);
      return json(saveDevice({ ...tv, needsPairing: false, online: true, state: { ...tv.state, on: false }, lastChangedAt: nowIso() }));
    },
  },
  { method: 'GET', path: '/api/scenes', handler: () => json(db.scenes) },
  {
    method: 'POST',
    path: '/api/scenes/{sceneId}/activate',
    handler: ({ params }) => {
      const scene = db.scenes.find((s) => s.id === params.sceneId);
      if (!scene) return fail(404, 'not_found', `Scene ${params.sceneId} not found`);
      const results: SceneActivation['results'] = scene.actions.map((action) => {
        const errorCode = runAction(action);
        return { action, ok: errorCode === null, errorCode };
      });
      return json({ sceneId: scene.id, results } satisfies SceneActivation);
    },
  },
  { method: 'GET', path: '/api/automations', handler: () => json([...db.automations].sort((a, b) => a.name.localeCompare(b.name))) },
  {
    method: 'GET',
    path: '/api/automations/runs',
    handler: ({ url }) => {
      const limit = Number(url.searchParams.get('limit') ?? 50);
      if (!isInt(limit, 1, 200)) return fail(422, 'validation_failed', 'limit must be a whole number from 1 to 200.');
      return json(db.runs.slice(0, limit));
    },
  },
  {
    method: 'GET',
    path: '/api/automations/{automationId}',
    handler: ({ params }) => found(db.automations.find((a) => a.id === params.automationId), `Automation ${params.automationId}`),
  },
  {
    method: 'PUT',
    path: '/api/automations/{automationId}/enabled',
    handler: async ({ params, request }) => {
      const automation = db.automations.find((a) => a.id === params.automationId);
      if (!automation) return fail(404, 'not_found', `Automation ${params.automationId} not found`);
      const { enabled } = await readBody(request);
      if (typeof enabled !== 'boolean') return fail(422, 'validation_failed', 'enabled must be true or false.');
      // Disabling cancels a waiting forMinutes trigger.
      return json(saveAutomation({ ...automation, enabled, waitingUntil: enabled ? automation.waitingUntil : null }));
    },
  },
  {
    method: 'POST',
    path: '/api/automations/{automationId}/simulate',
    handler: ({ params }) => found(simulate(params.automationId ?? ''), `Automation ${params.automationId}`),
  },
];

function matchPath(template: string, pathname: string): Record<string, string> | null {
  const expected = template.split('/');
  const actual = pathname.replace(/\/+$/, '').split('/');
  if (expected.length !== actual.length) return null;
  const params: Record<string, string> = {};
  for (const [i, segment] of expected.entries()) {
    const value = actual[i] ?? '';
    if (segment.startsWith('{') && segment.endsWith('}')) params[segment.slice(1, -1)] = decodeURIComponent(value);
    else if (segment !== value) return null;
  }
  return params;
}

export async function mockFetch(request: Request): Promise<Response> {
  if (LATENCY_MS > 0) await new Promise((resolve) => setTimeout(resolve, LATENCY_MS));
  const url = new URL(request.url);
  for (const route of mockRoutes) {
    if (route.method !== request.method) continue;
    const params = matchPath(route.path, url.pathname);
    if (params) return route.handler({ params, url, request });
  }
  return fail(404, 'not_found', `No mock route for ${request.method} ${url.pathname}`);
}
