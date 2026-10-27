import createClient from 'openapi-fetch';
import type { paths } from './generated/schema';
import { mockFetch } from './mock/mock-fetch';
import type { ApiErrorBody, DeviceCommand, DeviceReport, DeviceSettings, PairingRequest } from './types';

/**
 * The ONLY way the frontend talks to the backend.
 *
 * Paths, params and bodies are type-checked against contracts/openapi.yaml via the
 * generated `paths` type, so calling an endpoint that does not exist is a compile
 * error, not a runtime surprise. `mock` mode (the default) serves fixtures in the
 * browser so the UI runs without the .NET backend; `VITE_API_MODE=live` proxies
 * /api to the ASP.NET Core host on :5080 (see vite.config.ts).
 */
export const apiMode: 'mock' | 'live' = import.meta.env.VITE_API_MODE === 'live' ? 'live' : 'mock';

const http = createClient<paths>({
  baseUrl: globalThis.location?.origin ?? 'http://localhost',
  fetch: apiMode === 'mock' ? mockFetch : (request) => globalThis.fetch(request),
});

export class ApiError extends Error {
  readonly status: number;
  readonly code: string;

  constructor(status: number, code: string, message: string) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.code = code;
  }
}

async function unwrap<T>(call: Promise<{ data?: T; error?: unknown; response: Response }>): Promise<T> {
  const { data, error, response } = await call;
  if (response.ok && data !== undefined) return data;
  const body = error as Partial<ApiErrorBody> | undefined;
  throw new ApiError(
    response.status,
    body?.error?.code ?? 'http_error',
    body?.error?.message ?? `Request failed with status ${response.status}`,
  );
}

export const api = {
  health: {
    get: () => unwrap(http.GET('/api/health')),
  },
  presence: {
    get: () => unwrap(http.GET('/api/presence')),
  },
  rooms: {
    list: () => unwrap(http.GET('/api/rooms')),
  },
  devices: {
    list: (query: { roomId?: string } = {}) => unwrap(http.GET('/api/devices', { params: { query } })),
    get: (deviceId: string) => unwrap(http.GET('/api/devices/{deviceId}', { params: { path: { deviceId } } })),
    update: (deviceId: string, body: DeviceSettings) =>
      unwrap(http.PUT('/api/devices/{deviceId}', { params: { path: { deviceId } }, body })),
    command: (deviceId: string, body: DeviceCommand) =>
      unwrap(http.POST('/api/devices/{deviceId}/commands', { params: { path: { deviceId } }, body })),
    discover: () => unwrap(http.POST('/api/devices/discover')),
    /** What the phone's automation app sends; the browser only uses it in tests. */
    report: (deviceId: string, body: DeviceReport, token: string) =>
      unwrap(
        http.POST('/api/devices/{deviceId}/report', {
          params: { path: { deviceId } },
          body,
          headers: { Authorization: `Bearer ${token}` },
        }),
      ),
    startPairing: (deviceId: string, body: PairingRequest) =>
      unwrap(http.POST('/api/devices/{deviceId}/pairing', { params: { path: { deviceId } }, body })),
    confirmPairing: (deviceId: string, code: string) =>
      unwrap(http.POST('/api/devices/{deviceId}/pairing/confirm', { params: { path: { deviceId } }, body: { code } })),
  },
  scenes: {
    list: () => unwrap(http.GET('/api/scenes')),
    activate: (sceneId: string) =>
      unwrap(http.POST('/api/scenes/{sceneId}/activate', { params: { path: { sceneId } } })),
  },
  automations: {
    list: () => unwrap(http.GET('/api/automations')),
    get: (automationId: string) =>
      unwrap(http.GET('/api/automations/{automationId}', { params: { path: { automationId } } })),
    setEnabled: (automationId: string, enabled: boolean) =>
      unwrap(http.PUT('/api/automations/{automationId}/enabled', { params: { path: { automationId } }, body: { enabled } })),
    simulate: (automationId: string) =>
      unwrap(http.POST('/api/automations/{automationId}/simulate', { params: { path: { automationId } } })),
    runs: (query: { limit?: number } = {}) => unwrap(http.GET('/api/automations/runs', { params: { query } })),
  },
};
