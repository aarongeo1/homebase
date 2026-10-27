import { Link } from 'react-router';
import type { Device } from '@/shared/api';
import { Badge } from '@/shared/ui';
import { describeStatus } from '../model/presentation';

const ICON: Record<Device['type'], string> = { light: '💡', tv: '📺', phone: '📱' };

/** The TV and the phone: read-only here, Homebase watches them to run automations. */
export function StatusTile({ device }: { device: Device }) {
  return (
    <article className="card tile" aria-label={device.name}>
      <header className="tile__header">
        <span className="tile__icon" aria-hidden="true">
          {ICON[device.type]}
        </span>
        <div>
          <h3 className="tile__name">{device.name}</h3>
          <div className="muted tile__state">{describeStatus(device)}</div>
        </div>
      </header>
      <div className="row tile__badges">
        {!device.online && <Badge tone="danger">Offline</Badge>}
        {device.needsPairing && <Badge tone="warning">Not paired</Badge>}
      </div>
      {device.needsPairing && <Link to="/presence#pairing">Pair it on the Presence page →</Link>}
    </article>
  );
}
