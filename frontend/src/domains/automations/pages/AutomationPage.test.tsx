import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/shared/api';
import { renderAt } from '@/test/render';

/** Pins "now" (household time = the test's local time) without faking the timers user-event relies on. */
const setClock = (hours: number, minutes = 0) => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 27, hours, minutes));
};

afterEach(() => {
  vi.useRealTimers();
});

async function simulate(path: string) {
  const { user } = renderAt(path);
  await user.click(await screen.findByRole('button', { name: 'Simulate now' }));
  return screen.findByRole('region', { name: 'Simulation' });
}

describe('AutomationPage', () => {
  it('shows the rule in plain English, as the server words it', async () => {
    renderAt('/automations/woke-up');
    expect(await screen.findByText('When Phone wakes up')).toBeInTheDocument();
    expect(screen.getByText('Between 05:00 and 11:00')).toBeInTheDocument();
    expect(screen.getByText('It is dark')).toBeInTheDocument();
    expect(screen.getByText('Bedroom lights → on at 60%, 4000 K')).toBeInTheDocument();
  });

  it('would run after dark, and lists what it would do', async () => {
    setClock(23, 10);
    const simulation = await simulate('/automations/arrive-after-dark');
    expect(within(simulation).getByText('Would run')).toBeInTheDocument();
    expect(within(simulation).getByText('Condition: It is dark')).toBeInTheDocument();
    expect(within(simulation).getByText('Living room lights → on at 70%, 2700 K')).toBeInTheDocument();
  });

  it('would not run in daylight, and says which condition failed', async () => {
    setClock(12, 0);
    const simulation = await simulate('/automations/arrive-after-dark');
    expect(within(simulation).getByText('Would not run')).toBeInTheDocument();
    expect(within(simulation).getByLabelText('failed')).toBeInTheDocument();
    expect(within(simulation).queryByText('Would do')).not.toBeInTheDocument();
  });

  it('simulates a forMinutes trigger as if it had held, without changing anything', async () => {
    setClock(23, 0);
    const simulation = await simulate('/automations/charging-at-night');
    expect(within(simulation).getByText('Would run')).toBeInTheDocument();
    expect(within(simulation).getByText(/as if the trigger fired now and held for 15 min/)).toBeInTheDocument();
    expect(within(simulation).getByText('Bedroom lights → off')).toBeInTheDocument();
    expect((await api.devices.get('light-floor-lamp')).state.on).toBe(true);
  });

  it('shows when a waiting automation will fire', async () => {
    setClock(22, 10);
    await api.devices.report('phone', { charging: true }, 'mock-report-token');
    renderAt('/automations/charging-at-night');
    expect(await screen.findByText(/Waiting — fires at 22:25 if still charging/)).toBeInTheDocument();
  });

  it('shows no countdown when nothing is waiting', async () => {
    renderAt('/automations/charging-at-night');
    expect(await screen.findByText('When Phone has been charging for 15 min')).toBeInTheDocument();
    expect(screen.queryByText(/Waiting — fires at/)).not.toBeInTheDocument();
  });
});
