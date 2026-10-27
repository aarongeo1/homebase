// Timestamps arrive in UTC; we show them in the viewer's local time, which for a home hub is household time.
const HOUR_MS = 3_600_000;

export function formatDate(iso: string, locale = 'en-US'): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium' }).format(new Date(iso));
}

export function formatDateTime(iso: string, locale = 'en-US'): string {
  return new Intl.DateTimeFormat(locale, { dateStyle: 'medium', timeStyle: 'short' }).format(
    new Date(iso),
  );
}

/** 24-hour clock time, e.g. "23:25" — the same style as the API's household-local "HH:mm" values. */
export function formatTime(iso: string, locale = 'en-US'): string {
  return new Intl.DateTimeFormat(locale, { hour: '2-digit', minute: '2-digit', hourCycle: 'h23' }).format(new Date(iso));
}

/** "45m ago", "5h ago", "3d ago" — `now` is explicit so callers and tests control time. */
export function formatRelative(iso: string, now: Date): string {
  const hours = (now.getTime() - new Date(iso).getTime()) / HOUR_MS;
  if (hours < 1) return `${Math.max(0, Math.round(hours * 60))}m ago`;
  if (hours < 48) return `${Math.round(hours)}h ago`;
  return `${Math.round(hours / 24)}d ago`;
}

export function formatHours(hours: number): string {
  return Number.isInteger(hours) ? `${hours}h` : `${hours.toFixed(1)}h`;
}
