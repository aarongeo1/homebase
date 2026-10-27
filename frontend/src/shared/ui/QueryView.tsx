import type { ReactNode } from 'react';
import type { QueryState } from '@/shared/lib';
import { EmptyState, ErrorState, LoadingState } from './States';

export interface QueryViewProps<T> {
  query: QueryState<T>;
  children: (data: T) => ReactNode;
  isEmpty?: (data: T) => boolean;
  emptyMessage?: string;
}

/** Standard loading / error / empty handling for anything fetched with useQuery. */
export function QueryView<T>({ query, children, isEmpty, emptyMessage = 'Nothing here yet.' }: QueryViewProps<T>) {
  if (query.status === 'loading') return <LoadingState />;
  if (query.status === 'error') return <ErrorState message={query.error.message} onRetry={query.reload} />;
  if (isEmpty?.(query.data)) return <EmptyState message={emptyMessage} />;
  return <>{children(query.data)}</>;
}
