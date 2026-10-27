import { useEffect } from 'react';
import { useLocation } from 'react-router';
import { api } from '@/shared/api';
import { useQuery } from '@/shared/lib';
import { PageHeader, QueryView } from '@/shared/ui';
import { PhoneSetup } from '../components/PhoneSetup';
import { PresenceStatus } from '../components/PresenceStatus';
import { TvPairing } from '../components/TvPairing';

export function PresencePage() {
  const query = useQuery('presence-home', async () => {
    const presence = await api.presence.get();
    const tv = presence.tvDeviceId ? await api.devices.get(presence.tvDeviceId) : null;
    return { presence, tv };
  });
  useScrollToHash(query.status === 'success');

  return (
    <>
      <PageHeader title="Presence" subtitle="Whether you are home and whether it is dark — what your automations go by." />
      <QueryView query={query}>
        {({ presence, tv }) => (
          <div className="stack">
            <PresenceStatus presence={presence} />
            <TvPairing tv={tv} onPaired={query.reload} />
            <PhoneSetup phoneDeviceId={presence.phoneDeviceId} reportToken={presence.reportToken} />
          </div>
        )}
      </QueryView>
    </>
  );
}

/** Links such as /presence#pairing land on their section once the data has loaded. */
function useScrollToHash(ready: boolean) {
  const { hash } = useLocation();
  useEffect(() => {
    if (ready && hash) document.getElementById(hash.slice(1))?.scrollIntoView?.({ block: 'start' });
  }, [ready, hash]);
}
