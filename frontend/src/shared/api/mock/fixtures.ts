import type { Action, Automation, AutomationRun, Device, DeviceCommand, DeviceState, Room, Scene, Sun, Trigger } from '../types';

/**
 * Offline stand-in for the backend: the server's seed household (two rooms, the
 * phone and the Dining room TV) plus a few mock-only Govee lights so the UI has
 * something to control without hardware. Summaries and traces are written out
 * the way the server words them, not computed, because business rules belong to
 * the server. Times are relative to `now`.
 */
export interface MockData {
  rooms: Room[];
  devices: Device[];
  scenes: Scene[];
  automations: Automation[];
  runs: AutomationRun[];
  /** Sunrise/sunset as the mock reports them; `dark` is worked out per request. */
  sun: Omit<Sun, 'dark'>;
  reportToken: string;
  /** Set once the first "Find lights" has added the desk lamp, so later searches add nothing. */
  discovered: boolean;
}

const HOUR_MS = 3_600_000;
export const iso = (ms: number) => new Date(ms).toISOString().replace(/\.\d{3}Z$/, 'Z');

/** The API sends every state field; the ones a device type doesn't have are null. */
const deviceState = (fields: DeviceState): Required<DeviceState> => ({
  on: null,
  brightness: null,
  colorTempK: null,
  home: null,
  charging: null,
  asleep: null,
  ...fields,
});

export const light = (
  id: string,
  name: string,
  roomId: string | null,
  driver: Device['driver'],
  state: DeviceState,
  extra: Partial<Device> = {},
): Device => ({
  id,
  name,
  roomId,
  type: 'light',
  driver,
  online: true,
  needsPairing: false,
  state: deviceState(state),
  lastChangedAt: '2026-10-26T18:30:00Z',
  ...extra,
});

const roomAction = (roomId: string, summary: string, command: DeviceCommand): Action => ({
  kind: 'room',
  deviceId: null,
  roomId,
  sceneId: null,
  on: command.on ?? null,
  brightness: command.brightness ?? null,
  colorTempK: command.colorTempK ?? null,
  summary,
});

const sceneAction = (sceneId: string, summary: string): Action => ({
  kind: 'scene', deviceId: null, roomId: null, sceneId, on: null, brightness: null, colorTempK: null, summary,
});

const deviceTrigger = (
  deviceId: string,
  field: NonNullable<Trigger['field']>,
  equals: boolean,
  summary: string,
  forMinutes: number | null = null,
): Trigger => ({ kind: 'device', deviceId, field, equals, forMinutes, at: null, days: null, summary });

const IT_IS_DARK = { kind: 'dark', after: null, before: null, deviceId: null, field: null, equals: true, summary: 'It is dark' } as const;
const between = (after: string, before: string) =>
  ({ kind: 'time-window', after, before, deviceId: null, field: null, equals: null, summary: `Between ${after} and ${before}` }) as const;

type AutomationFields = Omit<Automation, 'enabled' | 'lastRunAt' | 'lastOutcome' | 'waitingUntil'> & Partial<Automation>;
const automation = (fields: AutomationFields): Automation => ({ enabled: true, lastRunAt: null, lastOutcome: null, waitingUntil: null, ...fields });

export function createFixtures(now: number = Date.now()): MockData {
  const hoursAgo = (h: number) => iso(now - h * HOUR_MS);

  const rooms: Room[] = [
    { id: 'living-room', name: 'Living room' },
    { id: 'bedroom', name: 'Bedroom' },
  ];

  const devices: Device[] = [
    {
      id: 'phone', name: 'Phone', roomId: null, type: 'phone', driver: 'push', online: true, needsPairing: false,
      state: deviceState({ home: true, charging: false, asleep: false }), lastChangedAt: hoursAgo(2),
    },
    {
      id: 'tv', name: 'Dining room TV', roomId: 'living-room', type: 'tv', driver: 'android-tv', online: false, needsPairing: true,
      state: deviceState({ on: null }), lastChangedAt: hoursAgo(48),
    },
    light('light-floor-lamp', 'Floor lamp', 'living-room', 'govee-lan', { on: true, brightness: 70, colorTempK: 2700 }, { lastChangedAt: hoursAgo(2) }),
    // Mock-only: offline, so the disabled state is visible without hardware.
    light('light-tv-strip', 'TV backlight', 'living-room', 'govee-cloud', { on: false, brightness: 40, colorTempK: 2700 }, { online: false, lastChangedAt: hoursAgo(1) }),
    light('light-bedside', 'Bedside lamp', 'bedroom', 'govee-lan', { on: false, brightness: 25, colorTempK: 2200 }, { lastChangedAt: hoursAgo(9) }),
  ];

  const scenes: Scene[] = [
    {
      id: 'all-off',
      name: 'All off',
      description: 'Every light off.',
      actions: [
        roomAction('living-room', 'Living room lights → off', { on: false }),
        roomAction('bedroom', 'Bedroom lights → off', { on: false }),
      ],
    },
    {
      id: 'evening',
      name: 'Evening',
      description: 'Warm light in the living room.',
      actions: [roomAction('living-room', 'Living room lights → on at 70%, 2700 K', { on: true, brightness: 70, colorTempK: 2700 })],
    },
    {
      id: 'bedtime',
      name: 'Bedtime',
      description: 'Living room off, a dim warm light by the bed.',
      actions: [
        roomAction('living-room', 'Living room lights → off', { on: false }),
        roomAction('bedroom', 'Bedroom lights → on at 25%, 2200 K', { on: true, brightness: 25, colorTempK: 2200 }),
      ],
    },
  ];

  const automations: Automation[] = [
    automation({
      id: 'leave-all-off',
      name: 'Leaving home → all off',
      description: 'Nothing stays on when you go out.',
      trigger: deviceTrigger('phone', 'home', false, 'When Phone leaves home'),
      conditions: [],
      actions: [sceneAction('all-off', 'Activate scene All off')],
      lastRunAt: hoursAgo(4),
      lastOutcome: 'executed',
    }),
    automation({
      id: 'arrive-after-dark',
      name: 'Home after dark → living room on',
      description: 'Come home to a lit living room once the sun is down.',
      trigger: deviceTrigger('phone', 'home', true, 'When Phone arrives home'),
      conditions: [IT_IS_DARK],
      actions: [roomAction('living-room', 'Living room lights → on at 70%, 2700 K', { on: true, brightness: 70, colorTempK: 2700 })],
      lastRunAt: hoursAgo(2),
      lastOutcome: 'executed',
    }),
    automation({
      id: 'tv-on-after-dark',
      name: 'TV on after dark → living room on',
      description: 'Some light in the room while the TV is on in the evening.',
      trigger: deviceTrigger('tv', 'on', true, 'When Dining room TV turns on'),
      conditions: [IT_IS_DARK],
      actions: [roomAction('living-room', 'Living room lights → on at 40%, 2700 K', { on: true, brightness: 40, colorTempK: 2700 })],
    }),
    automation({
      id: 'tv-off-late',
      name: 'TV off late → bedtime',
      description: 'Turning the TV off late at night means bedtime.',
      trigger: deviceTrigger('tv', 'on', false, 'When Dining room TV turns off'),
      conditions: [between('22:00', '03:00')],
      actions: [sceneAction('bedtime', 'Activate scene Bedtime')],
    }),
    automation({
      id: 'charging-at-night',
      name: 'Phone charging at night → bedroom off',
      description: 'Plugging the phone in at night means lights out.',
      trigger: deviceTrigger('phone', 'charging', true, 'When Phone has been charging for 15 min', 15),
      conditions: [between('21:00', '05:00')],
      actions: [roomAction('bedroom', 'Bedroom lights → off', { on: false })],
      lastRunAt: hoursAgo(16),
      lastOutcome: 'cancelled',
    }),
    automation({
      id: 'woke-up',
      name: 'Woke up → bedroom on',
      description: 'A bright, cool light on dark mornings.',
      trigger: deviceTrigger('phone', 'asleep', false, 'When Phone wakes up'),
      conditions: [between('05:00', '11:00'), IT_IS_DARK],
      actions: [roomAction('bedroom', 'Bedroom lights → on at 60%, 4000 K', { on: true, brightness: 60, colorTempK: 4000 })],
      lastRunAt: hoursAgo(9),
      lastOutcome: 'skipped',
    }),
    automation({
      id: 'went-to-sleep',
      name: 'Went to sleep → all off',
      description: 'Everything off once you are asleep.',
      trigger: deviceTrigger('phone', 'asleep', true, 'When Phone goes to sleep'),
      conditions: [],
      actions: [sceneAction('all-off', 'Activate scene All off')],
      lastRunAt: hoursAgo(17),
      lastOutcome: 'executed',
    }),
  ];

  const runs: AutomationRun[] = [
    {
      id: 'run_005', automationId: 'arrive-after-dark', automationName: 'Home after dark → living room on', firedAt: hoursAgo(2), outcome: 'executed', actionsExecuted: 1,
      trace: [
        { label: 'Trigger: When Phone arrives home', passed: true, detail: 'home changed false → true' },
        { label: 'Condition: It is dark', passed: true, detail: 'Now is 18:52; no location set, so dark means 18:00–07:00' },
      ],
    },
    {
      id: 'run_004', automationId: 'leave-all-off', automationName: 'Leaving home → all off', firedAt: hoursAgo(4), outcome: 'executed', actionsExecuted: 1,
      trace: [{ label: 'Trigger: When Phone leaves home', passed: true, detail: 'home changed true → false' }],
    },
    {
      id: 'run_003', automationId: 'woke-up', automationName: 'Woke up → bedroom on', firedAt: hoursAgo(9), outcome: 'skipped', actionsExecuted: 0,
      trace: [
        { label: 'Trigger: When Phone wakes up', passed: true, detail: 'asleep changed true → false' },
        { label: 'Condition: Between 05:00 and 11:00', passed: true, detail: 'Now is 07:40 household time' },
        { label: 'Condition: It is dark', passed: false, detail: 'Now is 07:40; no location set, so it is light from 07:00' },
      ],
    },
    {
      id: 'run_002', automationId: 'charging-at-night', automationName: 'Phone charging at night → bedroom off', firedAt: hoursAgo(16), outcome: 'cancelled', actionsExecuted: 0,
      trace: [{ label: 'Trigger: When Phone has been charging for 15 min', passed: false, detail: 'charging changed back to false after 6 min' }],
    },
    {
      id: 'run_001', automationId: 'went-to-sleep', automationName: 'Went to sleep → all off', firedAt: hoursAgo(17), outcome: 'executed', actionsExecuted: 1,
      trace: [{ label: 'Trigger: When Phone goes to sleep', passed: true, detail: 'asleep changed false → true' }],
    },
  ];

  return {
    rooms,
    devices,
    scenes,
    automations,
    runs,
    // Like a fresh install: no HOMEBASE_LATITUDE/LONGITUDE, so no sun times and dark means 18:00–07:00.
    sun: { sunrise: null, sunset: null, locationSet: false },
    reportToken: 'mock-report-token',
    discovered: false,
  };
}
