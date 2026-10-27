import type { RouteObject } from 'react-router';

/**
 * The contract every area of the app implements. The app shell (src/app) only
 * knows about domains through this shape; see ADR-0001.
 */
export interface DomainModule {
  id: string;
  title: string;
  /** Who this area serves, e.g. "Shoppers". Shown in the UI and used in agent context. */
  audience: string;
  description: string;
  /** URL segment without slashes, e.g. "shop". */
  basePath: string;
  nav: ReadonlyArray<{ label: string; to: string }>;
  /** Routes relative to basePath. */
  routes: RouteObject[];
}
