import type { DomainModule } from '@/shared/lib';
import { PresencePage } from './pages/PresencePage';
import './presence.css';

export const presenceModule: DomainModule = {
  id: 'presence',
  title: 'Presence',
  audience: 'Whoever carries the phone',
  description: 'Home or away, whether it is dark, pairing the TV and setting up the phone to report in.',
  basePath: 'presence',
  nav: [{ label: 'Home & away', to: '/presence' }],
  routes: [{ index: true, element: <PresencePage /> }],
};
