import type { RunOutcome } from '@/shared/api';
import { Badge, type BadgeTone } from '@/shared/ui';

const TONE: Record<RunOutcome, BadgeTone> = { executed: 'success', skipped: 'neutral', cancelled: 'info', failed: 'danger' };

export function OutcomeBadge({ outcome }: { outcome: RunOutcome }) {
  return <Badge tone={TONE[outcome]}>{outcome}</Badge>;
}
