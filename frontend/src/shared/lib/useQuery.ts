import { useCallback, useEffect, useRef, useState } from 'react';

export type QueryState<T> =
  | { status: 'loading'; reload: () => void }
  | { status: 'success'; data: T; reload: () => void }
  | { status: 'error'; error: Error; reload: () => void };

type Settled<T> = { status: 'success'; data: T } | { status: 'error'; error: Error };

/**
 * Loads data for a component. `key` identifies the request: when it changes the
 * query re-runs, shows a loading state and ignores stale responses. `reload()`
 * re-runs the same key but keeps showing the current data until the new response
 * arrives, so components below never unmount (and never lose local state) on refresh.
 * Render the result with <QueryView> from @/shared/ui.
 */
export function useQuery<T>(key: string, load: () => Promise<T>): QueryState<T> {
  const [nonce, setNonce] = useState(0);
  const [settled, setSettled] = useState<{ key: string; nonce: number; result: Settled<T> } | null>(null);
  const loadRef = useRef(load);

  useEffect(() => {
    loadRef.current = load;
  });

  useEffect(() => {
    let cancelled = false;
    loadRef.current().then(
      (data) => {
        if (!cancelled) setSettled({ key, nonce, result: { status: 'success', data } });
      },
      (error: unknown) => {
        const asError = error instanceof Error ? error : new Error(String(error));
        if (!cancelled) setSettled({ key, nonce, result: { status: 'error', error: asError } });
      },
    );
    return () => {
      cancelled = true;
    };
  }, [key, nonce]);

  const reload = useCallback(() => setNonce((n) => n + 1), []);

  if (!settled || settled.key !== key) return { status: 'loading', reload };
  return { ...settled.result, reload };
}
