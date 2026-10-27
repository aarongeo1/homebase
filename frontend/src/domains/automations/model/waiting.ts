import type { Automation, TriggerField } from '@/shared/api';
import { formatTime } from '@/shared/lib';

/** How a trigger field reads when it is true / false, e.g. charging → "charging" / "unplugged". */
const STATE_WORDS: Record<TriggerField, readonly [string, string]> = {
  on: ['on', 'off'],
  home: ['home', 'away'],
  charging: ['charging', 'unplugged'],
  asleep: ['asleep', 'awake'],
};

/** "Waiting — fires at 23:25 if still charging", from the server's waitingUntil; null when not waiting. */
export function describeWaiting({ waitingUntil, trigger }: Pick<Automation, 'waitingUntil' | 'trigger'>): string | null {
  if (!waitingUntil) return null;
  const state = trigger.field != null && trigger.equals != null ? STATE_WORDS[trigger.field][trigger.equals ? 0 : 1] : null;
  return `Waiting — fires at ${formatTime(waitingUntil)}${state ? ` if still ${state}` : ''}`;
}
