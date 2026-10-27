import type { Presence } from '@/shared/api';
import { Card, StatCard } from '@/shared/ui';

/** Home/away and darkness exactly as the server reports them. */
export function PresenceStatus({ presence }: { presence: Presence }) {
  const { home, sun } = presence;
  return (
    <Card title="Right now">
      <div className="grid grid--stats">
        <StatCard
          label="You are"
          value={home === null ? 'Unknown' : home ? 'Home' : 'Away'}
          hint={home === null ? "Phone hasn't reported yet" : undefined}
        />
        <StatCard label="Outside" value={sun.dark ? 'Dark' : 'Light'} />
        <StatCard label="Sunrise" value={sun.sunrise ?? '—'} />
        <StatCard label="Sunset" value={sun.sunset ?? '—'} />
      </div>
      {!sun.locationSet && (
        <p className="muted presence__hint">
          No location is set, so “dark” means 18:00–07:00. For real sunrise and sunset, set <code>HOMEBASE_LATITUDE</code> and{' '}
          <code>HOMEBASE_LONGITUDE</code> on the Mac.
        </p>
      )}
    </Card>
  );
}
