import type { Automation } from '@/shared/api';
import { describeWaiting } from '../model/waiting';

/** Shown while a forMinutes trigger is counting down (the server sets waitingUntil). */
export function WaitingNotice({ automation }: { automation: Automation }) {
  const text = describeWaiting(automation);
  return text ? <p className="automation__waiting">⏳ {text}</p> : null;
}
