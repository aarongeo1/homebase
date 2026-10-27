import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { api, type Automation, type Simulation } from '@/shared/api';
import { useQuery } from '@/shared/lib';
import { Badge, Button, Card, PageHeader, QueryView } from '@/shared/ui';
import { TraceList } from '../components/TraceList';
import { WaitingNotice } from '../components/WaitingNotice';

export function AutomationPage() {
  const { automationId = '' } = useParams();
  const query = useQuery(`automation:${automationId}`, () => api.automations.get(automationId));
  return <QueryView query={query}>{(automation) => <AutomationDetail automation={automation} />}</QueryView>;
}

function AutomationDetail({ automation }: { automation: Automation }) {
  const [simulation, setSimulation] = useState<Simulation | null>(null);
  const [running, setRunning] = useState(false);

  async function simulate() {
    setRunning(true);
    try {
      setSimulation(await api.automations.simulate(automation.id));
    } finally {
      setRunning(false);
    }
  }

  return (
    <>
      <PageHeader
        title={automation.name}
        subtitle={automation.description}
        actions={
          <>
            <Link to="/automations">← All automations</Link>
            <Button onClick={simulate} disabled={running}>
              {running ? 'Simulating…' : 'Simulate now'}
            </Button>
          </>
        }
      />
      <div className="stack">
        <Card title="Rule">
          <dl className="rule">
            <dt>When</dt>
            <dd>{automation.trigger.summary}</dd>
            <dt>Only if</dt>
            <dd>
              {automation.conditions.length === 0 ? (
                <span className="muted">No conditions</span>
              ) : (
                <ul>
                  {automation.conditions.map((c, i) => (
                    <li key={i}>{c.summary}</li>
                  ))}
                </ul>
              )}
            </dd>
            <dt>Then</dt>
            <dd>
              <ul>
                {automation.actions.map((a, i) => (
                  <li key={i}>{a.summary}</li>
                ))}
              </ul>
            </dd>
          </dl>
          <WaitingNotice automation={automation} />
          {!automation.enabled && <Badge>Disabled — will not run on its own</Badge>}
        </Card>
        {simulation && (
          <Card
            title="Simulation"
            actions={<Badge tone={simulation.wouldRun ? 'success' : 'warning'}>{simulation.wouldRun ? 'Would run' : 'Would not run'}</Badge>}
          >
            <p className="muted">
              A dry run as if the trigger fired now
              {automation.trigger.forMinutes ? ` and held for ${automation.trigger.forMinutes} min` : ''}. Nothing was changed.
            </p>
            <TraceList steps={simulation.trace} />
            {simulation.plannedActions.length > 0 && (
              <>
                <h3 className="sim__heading">Would do</h3>
                <ul>
                  {simulation.plannedActions.map((a, i) => (
                    <li key={i}>{a.summary}</li>
                  ))}
                </ul>
              </>
            )}
          </Card>
        )}
      </div>
    </>
  );
}
