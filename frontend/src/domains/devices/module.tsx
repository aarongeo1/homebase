import type { DomainModule } from '@/shared/lib';
import { DevicesPage } from './pages/DevicesPage';
import './devices.css';

export const devicesModule: DomainModule = {
  id: 'devices',
  title: 'Devices',
  audience: 'Anyone at home',
  description: 'Lights by room with brightness and warmth, the TV and phone, and one-tap scenes.',
  basePath: 'devices',
  nav: [{ label: 'Lights', to: '/devices' }],
  routes: [{ index: true, element: <DevicesPage /> }],
};
