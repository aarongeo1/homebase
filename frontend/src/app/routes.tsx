import type { RouteObject } from 'react-router';
import { domainModules } from './domain-registry';
import { NotFoundPage, RouteErrorPage } from './ErrorPages';
import { HomePage } from './HomePage';
import { Layout } from './Layout';

export const appRoutes: RouteObject[] = [
  {
    path: '/',
    element: <Layout />,
    errorElement: <RouteErrorPage />,
    children: [
      { index: true, element: <HomePage /> },
      ...domainModules.map((m) => ({ path: m.basePath, children: m.routes })),
      { path: '*', element: <NotFoundPage /> },
    ],
  },
];
