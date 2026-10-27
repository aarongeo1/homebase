import type { TraceStep } from '@/shared/api';

/** Renders the server's explanation of an automation decision, step by step. */
export function TraceList({ steps }: { steps: TraceStep[] }) {
  return (
    <ol className="trace">
      {steps.map((step, i) => (
        <li key={i} className={step.passed ? 'trace__step trace__step--pass' : 'trace__step trace__step--fail'}>
          <span className="trace__mark" aria-label={step.passed ? 'passed' : 'failed'}>
            {step.passed ? '✓' : '✗'}
          </span>
          <div>
            <div>{step.label}</div>
            <div className="muted">{step.detail}</div>
          </div>
        </li>
      ))}
    </ol>
  );
}
