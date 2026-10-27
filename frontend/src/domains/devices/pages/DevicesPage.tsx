import { useState } from 'react';
import { api, type Device } from '@/shared/api';
import { useQuery } from '@/shared/lib';
import { Card, PageHeader, QueryView } from '@/shared/ui';
import { FindLights } from '../components/FindLights';
import { LightTile } from '../components/LightTile';
import { SceneBar } from '../components/SceneBar';
import { StatusTile } from '../components/StatusTile';
import { groupLights } from '../model/presentation';

export function DevicesPage() {
  const query = useQuery('devices-home', async () => {
    const [rooms, devices, scenes] = await Promise.all([api.rooms.list(), api.devices.list(), api.scenes.list()]);
    return { rooms, devices, scenes };
  });
  // Commands and settings return the updated device; keep it locally instead of refetching everything.
  const [updated, setUpdated] = useState<Record<string, Device>>({});
  const remember = (next: Device) => setUpdated((u) => ({ ...u, [next.id]: next }));
  const refresh = () => {
    setUpdated({});
    query.reload();
  };

  return (
    <>
      <PageHeader title="Devices" subtitle="Your lights by room, and the TV and phone Homebase keeps an eye on." />
      <QueryView query={query}>
        {({ rooms, devices, scenes }) => {
          const current = devices.map((d) => updated[d.id] ?? d);
          const watched = current.filter((d) => d.type !== 'light');
          return (
            <div className="stack">
              <Card title="Scenes">
                <SceneBar scenes={scenes} onActivated={refresh} />
              </Card>
              {groupLights(
                current.filter((d) => d.type === 'light'),
                rooms,
              ).map((group) => (
                <section key={group.id} aria-labelledby={`room-${group.id}`} className="stack">
                  <h2 id={`room-${group.id}`} className="room-title">
                    {group.name}
                  </h2>
                  {group.lights.length === 0 ? (
                    <p className="muted">No lights here yet.</p>
                  ) : (
                    <div className="grid grid--tiles">
                      {group.lights.map((d) => (
                        <LightTile key={`${d.id}@${d.lastChangedAt}`} device={d} rooms={rooms} onChange={remember} />
                      ))}
                    </div>
                  )}
                </section>
              ))}
              <FindLights onFound={refresh} />
              {watched.length > 0 && (
                <section aria-labelledby="room-watched" className="stack">
                  <div>
                    <h2 id="room-watched" className="room-title">
                      TV and phone
                    </h2>
                    <p className="muted">Read-only here: Homebase watches them to run your automations.</p>
                  </div>
                  <div className="grid grid--tiles">
                    {watched.map((d) => (
                      <StatusTile key={d.id} device={d} />
                    ))}
                  </div>
                </section>
              )}
            </div>
          );
        }}
      </QueryView>
    </>
  );
}
