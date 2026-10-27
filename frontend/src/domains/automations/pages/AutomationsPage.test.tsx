import { screen, waitFor, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/shared/api';
import { renderAt } from '@/test/render';

afterEach(() => {
  vi.useRealTimers();
});

describe('AutomationsPage', () => {
  it('lists the automations with their triggers', async () => {
    renderAt('/automations');
    expect(await screen.findByRole('link', { name: 'Leaving home → all off' })).toBeInTheDocument();
    expect(screen.getByText('When Phone has been charging for 15 min')).toBeInTheDocument();
    expect(screen.getByText('When Dining room TV turns off')).toBeInTheDocument();
  });

  it('shows cancelled runs in the history with the reason', async () => {
    renderAt('/automations');
    const runs = await screen.findByRole('region', { name: 'Recent runs' });
    expect(within(runs).getByText('cancelled')).toBeInTheDocument();
    expect(within(runs).getByText('charging changed back to false after 6 min')).toBeInTheDocument();
  });

  it('shows a waiting countdown, and disabling the automation stops it', async () => {
    vi.useFakeTimers({ toFake: ['Date'] });
    vi.setSystemTime(new Date(2026, 9, 27, 23, 40));
    await api.devices.report('phone', { charging: true }, 'mock-report-token');
    const { user } = renderAt('/automations');
    expect(await screen.findByText(/Waiting — fires at 23:55 if still charging/)).toBeInTheDocument();

    await user.click(screen.getByRole('checkbox', { name: 'Enable Phone charging at night → bedroom off' }));
    await waitFor(() => expect(screen.queryByText(/Waiting — fires at/)).not.toBeInTheDocument());
  });
});
