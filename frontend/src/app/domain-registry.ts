import { automationsModule } from '@/domains/automations';
import { devicesModule } from '@/domains/devices';
import { presenceModule } from '@/domains/presence';
import type { DomainModule } from '@/shared/lib';

/** Every area of Homebase. Add new domains here (see the scaffold-domain skill). */
export const domainModules: readonly DomainModule[] = [devicesModule, automationsModule, presenceModule];
