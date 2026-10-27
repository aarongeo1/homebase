import { describe, expect, it } from 'vitest';
import { parse } from 'yaml';
import contractYaml from '../../../../../contracts/openapi.yaml?raw';
import { api } from '../client';
import { mockRoutes } from './mock-fetch';

const HTTP_METHODS = ['get', 'post', 'put', 'patch', 'delete'];

describe('mock API', () => {
  it('implements every operation in contracts/openapi.yaml, and nothing else', () => {
    const spec = parse(contractYaml) as { paths: Record<string, Record<string, unknown>> };
    const contract = Object.entries(spec.paths)
      .flatMap(([path, ops]) => Object.keys(ops).filter((m) => HTTP_METHODS.includes(m)).map((m) => `${m.toUpperCase()} ${path}`))
      .sort();
    const mocked = mockRoutes.map((r) => `${r.method} ${r.path}`).sort();
    expect(mocked).toEqual(contract);
  });

  it('accepts phone reports only with the report token', async () => {
    await expect(api.devices.report('phone', { home: false }, 'wrong-token')).rejects.toMatchObject({ status: 401, code: 'unauthorized' });
    expect((await api.presence.get()).home).toBe(true);
    const phone = await api.devices.report('phone', { home: false }, 'mock-report-token');
    expect(phone.state.home).toBe(false);
    expect((await api.presence.get()).home).toBe(false);
  });

  it('accepts reports only from the phone', async () => {
    await expect(api.devices.report('tv', { home: true }, 'mock-report-token')).rejects.toMatchObject({ status: 422, code: 'not_reportable' });
  });

  it('treats the TV and phone as read-only and only pairs the TV', async () => {
    await expect(api.devices.command('tv', { on: true })).rejects.toMatchObject({ status: 422, code: 'read_only_device' });
    await expect(api.devices.command('phone', { on: true })).rejects.toMatchObject({ status: 422, code: 'read_only_device' });
    await expect(api.devices.startPairing('light-floor-lamp', {})).rejects.toMatchObject({ status: 422, code: 'not_pairable' });
  });

  it('brings the TV online once it is paired', async () => {
    await api.devices.startPairing('tv', { address: null });
    const tv = await api.devices.confirmPairing('tv', 'A1B2C3');
    expect(tv).toMatchObject({ needsPairing: false, online: true, state: { on: false } });
  });
});
