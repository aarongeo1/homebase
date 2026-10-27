import { useState } from 'react';
import { Link } from 'react-router';
import { api, type Automation } from '@/shared/api';
import { useQuery } from '@/shared/lib';
import { Card, PageHeader, QueryView } from '@/shared/ui';
import { OutcomeBadge } from '../components/OutcomeBadge';
import { RunsList } from '../components/RunsList';
import { WaitingNotice } from '../components/WaitingNotice';

export function AutomationsPage() {
  const query = useQuery('automations-home', async () => {
    const [automations, runs] = await Promise.all([api.automations.list(), api.automations.runs({ limit: 20 })]);
    return { automations, runs };
  });
  const [toggled, setToggled] = useState<Record<string, Automation>>({});

  async function toggle(automation: Automation) {
    const next = await api.automations.setEnabled(automation.id, !automation.enabled);
    setToggled((t) => ({ ...t, [next.id]: next }));
  }

  return (
    <>
      <PageHeader title="Automations" subtitle="When something happens and the conditions hold, Homebase acts — and keeps a trace of why." />
      <QueryView query={query}>
        {({ automations, runs }) => (
          <div className="stack">
            <div className="grid grid--cards">
              {automations.map((original) => {
                const a = toggled[original.id] ?? original;
                return (
                  <article key={a.id} className={`card automation${a.enabled ? '' : ' automation--off'}`}>
                    <header className="card__header">
                      <h2 className="card__title">
                        <Link to={`/automations/${a.id}`}>{a.name}</Link>
                      </h2>
                      <label className="switch">
                        <input type="checkbox" checked={a.enabled} onChange={() => toggle(a)} aria-label={`Enable ${a.name}`} />
                        <span>{a.enabled ? 'On' : 'Off'}</span>
                      </label>
                    </header>
                    <p className="muted">{a.description}</p>
                    <p className="automation__when">
                      <strong>When</strong> {a.trigger.summary}
                    </p>
                    <WaitingNotice automation={a} />
                    {a.lastOutcome && (
                      <div className="row">
                        Last run <OutcomeBadge outcome={a.lastOutcome} />
                      </div>
                    )}
                  </article>
                );
              })}
            </div>
            <Card title="Recent runs">
              <RunsList runs={runs} />
            </Card>
          </div>
        )}
      </QueryView>
    </>
  );
}
