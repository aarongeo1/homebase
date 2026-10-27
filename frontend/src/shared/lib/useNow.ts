import { useEffect, useState } from 'react';

/** Current time, refreshed every `intervalMs`. Keeps impure `new Date()` out of render. */
export function useNow(intervalMs = 60_000): Date {
  const [now, setNow] = useState(() => new Date());
  useEffect(() => {
    const id = setInterval(() => setNow(new Date()), intervalMs);
    return () => clearInterval(id);
  }, [intervalMs]);
  return now;
}
