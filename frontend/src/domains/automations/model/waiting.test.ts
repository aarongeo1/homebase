import { describe, expect, it } from 'vitest';
import type { Trigger } from '@/shared/api';
import { describeWaiting } from './waiting';

const trigger = (field: Trigger['field'], equals: boolean): Trigger => ({ kind: 'device', deviceId: 'phone', field, equals, forMinutes: 15, summary: '' });
const at = (hours: number, minutes: number) => new Date(2026, 9, 27, hours, minutes).toISOString();

describe('describeWaiting', () => {
  it('says when the automation fires and what must still hold', () => {
    expect(describeWaiting({ waitingUntil: at(23, 25), trigger: trigger('charging', true) })).toBe('Waiting — fires at 23:25 if still charging');
    expect(describeWaiting({ waitingUntil: at(0, 5), trigger: trigger('home', false) })).toBe('Waiting — fires at 00:05 if still away');
    expect(describeWaiting({ waitingUntil: at(7, 0), trigger: trigger('asleep', false) })).toBe('Waiting — fires at 07:00 if still awake');
  });

  it('is null when nothing is counting down', () => {
    expect(describeWaiting({ waitingUntil: null, trigger: trigger('charging', true) })).toBeNull();
  });
});
