import type { AutomationRun } from '@/shared/api';
import { formatRelative, useNow } from '@/shared/lib';
import { OutcomeBadge } from './OutcomeBadge';
import { TraceList } from './TraceList';

export function RunsList({ runs }: { runs: AutomationRun[] }) {
  const now = useNow();
  return (
    <ul className="runs">
      {runs.map((run) => (
        <li key={run.id}>
          <details>
            <summary className="row">
              <OutcomeBadge outcome={run.outcome} />
              <strong>{run.automationName}</strong>
              <span className="muted">
                {formatRelative(run.firedAt, now)} · {run.actionsExecuted} action(s)
              </span>
            </summary>
            <TraceList steps={run.trace} />
          </details>
        </li>
      ))}
    </ul>
  );
}
