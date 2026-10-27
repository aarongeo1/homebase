import { describe, expect, it } from 'vitest';
import type { Device } from '@/shared/api';
import { describeLight, describeStatus, groupLights } from './presentation';

const lamp: Device = {
  id: 'l',
  name: 'Lamp',
  roomId: 'bedroom',
  type: 'light',
  driver: 'govee-lan',
  online: true,
  needsPairing: false,
  state: { on: true, brightness: 60, colorTempK: 2700 },
  lastChangedAt: '2026-01-01T00:00:00Z',
};
const rooms = [
  { id: 'living-room', name: 'Living room' },
  { id: 'bedroom', name: 'Bedroom' },
];

describe('device presentation', () => {
  it('describes a light', () => {
    expect(describeLight(lamp)).toBe('On · 60% · 2700 K');
    expect(describeLight({ ...lamp, state: { on: false, brightness: 60 } })).toBe('Off');
    expect(describeLight({ ...lamp, state: { on: null } })).toBe('Unknown');
  });

  it('describes the phone and the TV', () => {
    const phone: Device = { ...lamp, type: 'phone', driver: 'push', state: { home: false, charging: true, asleep: false } };
    expect(describeStatus(phone)).toBe('Away · Charging · Awake');
    expect(describeStatus({ ...phone, state: {} })).toBe("Hasn't reported yet");
    expect(describeStatus({ ...lamp, type: 'tv', driver: 'android-tv', state: { on: null } })).toBe('Unknown');
  });

  it('groups lights in room order and puts lights without a room last', () => {
    const groups = groupLights([{ ...lamp, id: 'a', roomId: null }, lamp], rooms);
    expect(groups.map((g) => [g.name, g.lights.map((l) => l.id)])).toEqual([
      ['Living room', []],
      ['Bedroom', ['l']],
      ['Not in a room', ['a']],
    ]);
  });

  it('leaves out "Not in a room" when every light has one', () => {
    expect(groupLights([lamp], rooms).map((g) => g.name)).toEqual(['Living room', 'Bedroom']);
  });
});
