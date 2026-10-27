import { isRouteErrorResponse, Link, useRouteError } from 'react-router';
import { EmptyState, ErrorState } from '@/shared/ui';

export function NotFoundPage() {
  return (
    <div className="stack">
      <EmptyState message="That page does not exist." />
      <Link to="/">Go home</Link>
    </div>
  );
}

export function RouteErrorPage() {
  const error = useRouteError();
  const message = isRouteErrorResponse(error) ? `${error.status} ${error.statusText}` : 'Something went wrong.';
  return <ErrorState message={message} />;
}
