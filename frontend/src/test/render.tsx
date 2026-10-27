import { render } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { createMemoryRouter, type RouteObject } from 'react-router';
import { RouterProvider } from 'react-router/dom';
import { appRoutes } from '@/app/routes';

/**
 * Renders the real app routes at `path` against the mock API.
 * Use this for every page/component test so tests exercise real routing.
 */
export function renderAt(path: string, routes: RouteObject[] = appRoutes) {
  const router = createMemoryRouter(routes, { initialEntries: [path] });
  const user = userEvent.setup();
  return { user, router, ...render(<RouterProvider router={router} />) };
}
