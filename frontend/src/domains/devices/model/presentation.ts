import type { Device, DeviceDriver, Room } from '@/shared/api';

/** The warmth slider's range: 2000 K is candle-warm, 6500 K is daylight. The API accepts up to 9000 K. */
export const WARMTH_MIN_K = 2000;
export const WARMTH_MAX_K = 6500;

export const DRIVER_LABEL: Record<DeviceDriver, string> = {
  simulated: 'Simulated',
  'govee-lan': 'Govee · local',
  'govee-cloud': 'Govee · cloud',
  'android-tv': 'Android TV',
  push: 'Reports in',
};

export interface LightGroup {
  id: string;
  name: string;
  lights: Device[];
}

/** Lights per room in the API's room order, then "Not in a room" (only when it has lights). */
export function groupLights(lights: Device[], rooms: Room[]): LightGroup[] {
  const groups: LightGroup[] = rooms.map((room) => ({ id: room.id, name: room.name, lights: lights.filter((l) => l.roomId === room.id) }));
  const known = new Set(rooms.map((r) => r.id));
  const unassigned = lights.filter((l) => l.roomId === null || !known.has(l.roomId));
  return unassigned.length > 0 ? [...groups, { id: 'none', name: 'Not in a room', lights: unassigned }] : groups;
}

/** One-line summary of a light, e.g. "On · 70% · 2700 K" or "Off". */
export function describeLight(device: Device): string {
  const { on, brightness, colorTempK } = device.state;
  if (on == null) return 'Unknown';
  if (!on) return 'Off';
  return ['On', brightness != null && `${brightness}%`, colorTempK != null && `${colorTempK} K`].filter(Boolean).join(' · ');
}

/** Why an offline light can't be controlled, based only on how Homebase talks to it. */
export function offlineReason(device: Device): string {
  switch (device.driver) {
    case 'govee-lan':
      return "Homebase can't reach it on your local network. Check it has power and Wi-Fi.";
    case 'govee-cloud':
      return "Homebase can't reach it through the Govee cloud. Check it has power and Wi-Fi.";
    default:
      return "Homebase can't reach it right now.";
  }
}

/** One-line summary of the TV or phone, e.g. "Home · Not charging · Awake". */
export function describeStatus(device: Device): string {
  const s = device.state;
  if (device.type === 'phone') {
    if (s.home == null && s.charging == null && s.asleep == null) return "Hasn't reported yet";
    return [
      s.home != null && (s.home ? 'Home' : 'Away'),
      s.charging != null && (s.charging ? 'Charging' : 'Not charging'),
      s.asleep != null && (s.asleep ? 'Asleep' : 'Awake'),
    ]
      .filter(Boolean)
      .join(' · ');
  }
  return s.on == null ? 'Unknown' : s.on ? 'On' : 'Off';
}
