/** Plain-English rows for "Set up your phone": what the phone sends to the report URL for each event. */
export const PHONE_REPORTS: ReadonlyArray<{ event: string; body: string }> = [
  { event: 'Left home', body: '{"home": false}' },
  { event: 'Arrived home', body: '{"home": true}' },
  { event: 'Started charging', body: '{"charging": true}' },
  { event: 'Stopped charging', body: '{"charging": false}' },
  { event: 'Went to sleep', body: '{"asleep": true}' },
  { event: 'Woke up', body: '{"asleep": false}' },
];

/** The URL the phone posts to; `origin` is wherever this page was opened from. */
export function reportUrl(origin: string, phoneDeviceId: string): string {
  return `${origin}/api/devices/${encodeURIComponent(phoneDeviceId)}/report`;
}
