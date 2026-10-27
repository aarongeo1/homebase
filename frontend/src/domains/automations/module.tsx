import type { DomainModule } from '@/shared/lib';
import { AutomationPage } from './pages/AutomationPage';
import { AutomationsPage } from './pages/AutomationsPage';
import './automations.css';

export const automationsModule: DomainModule = {
  id: 'automations',
  title: 'Automations',
  audience: 'Whoever sets up the rules',
  description: 'Rules like “leaving home → all off”, run history and a simulator that explains every decision.',
  basePath: 'automations',
  nav: [{ label: 'Rules & runs', to: '/automations' }],
  routes: [
    { index: true, element: <AutomationsPage /> },
    { path: ':automationId', element: <AutomationPage /> },
  ],
};
