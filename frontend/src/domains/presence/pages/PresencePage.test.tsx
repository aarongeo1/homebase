import { screen, within } from '@testing-library/react';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { api } from '@/shared/api';
import { renderAt } from '@/test/render';
import { PhoneSetup } from '../components/PhoneSetup';
import { PresenceStatus } from '../components/PresenceStatus';

const setClock = (hours: number) => {
  vi.useFakeTimers({ toFake: ['Date'] });
  vi.setSystemTime(new Date(2026, 9, 27, hours, 0));
};

afterEach(() => {
  vi.useRealTimers();
});

async function startPairing() {
  const result = renderAt('/presence#pairing');
  const pairing = await screen.findByRole('region', { name: 'Pair the TV' });
  await result.user.click(within(pairing).getByRole('button', { name: 'Show code on TV' }));
  await within(pairing).findByLabelText('Code shown on the TV');
  return { ...result, pairing };
}

describe('PresencePage', () => {
  it('shows that you are home, whether it is dark, and how to set a location', async () => {
    setClock(21);
    renderAt('/presence');
    const now = await screen.findByRole('region', { name: 'Right now' });
    expect(within(now).getByText('Home')).toBeInTheDocument();
    expect(within(now).getByText('Dark')).toBeInTheDocument();
    expect(within(now).getAllByText('—')).toHaveLength(2); // no location, so no sunrise or sunset
    expect(within(now).getByText('HOMEBASE_LATITUDE')).toBeInTheDocument();
    expect(within(now).getByText('HOMEBASE_LONGITUDE')).toBeInTheDocument();
    expect(within(now).queryByText("Phone hasn't reported yet")).not.toBeInTheDocument();
  });

  it('shows Away once the phone reports leaving, and Light during the day', async () => {
    setClock(12);
    await api.devices.report('phone', { home: false }, 'mock-report-token');
    renderAt('/presence');
    const now = await screen.findByRole('region', { name: 'Right now' });
    expect(within(now).getByText('Away')).toBeInTheDocument();
    expect(within(now).getByText('Light')).toBeInTheDocument();
  });

  it("says when the phone hasn't reported yet and sunrise is unknown", async () => {
    const presence = {
      home: null,
      phoneDeviceId: 'phone',
      tvDeviceId: null,
      reportToken: null,
      sun: { dark: false, sunrise: null, sunset: null, locationSet: true },
    };
    renderAt('/', [{ path: '/', element: <PresenceStatus presence={presence} /> }]);
    expect(await screen.findByText("Phone hasn't reported yet")).toBeInTheDocument();
    expect(screen.getByText('Unknown')).toBeInTheDocument();
    expect(screen.getAllByText('—')).toHaveLength(2);
    expect(screen.queryByText('HOMEBASE_LATITUDE')).not.toBeInTheDocument();
  });

  it('pairs the TV with the code it shows', async () => {
    const { user } = renderAt('/presence#pairing');
    const pairing = await screen.findByRole('region', { name: 'Pair the TV' });
    await user.type(within(pairing).getByLabelText(/TV address/), '192.168.1.40');
    await user.click(within(pairing).getByRole('button', { name: 'Show code on TV' }));
    expect(await within(pairing).findByText(/Dining room TV should now show a 6-character code/)).toBeInTheDocument();

    await user.type(within(pairing).getByLabelText('Code shown on the TV'), 'a1b2c3');
    await user.click(within(pairing).getByRole('button', { name: 'Pair' }));
    expect(await within(pairing).findByText('Dining room TV is paired.')).toBeInTheDocument();
    expect(within(pairing).getByText('Paired')).toBeInTheDocument();
  });

  it('shows the server error when the TV rejects the code', async () => {
    const { user, pairing } = await startPairing();
    await user.type(within(pairing).getByLabelText('Code shown on the TV'), '000000');
    await user.click(within(pairing).getByRole('button', { name: 'Pair' }));
    expect(await within(pairing).findByRole('alert')).toHaveTextContent('Dining room TV did not accept that code.');
  });

  it('shows the server error for a malformed code', async () => {
    const { user, pairing } = await startPairing();
    await user.type(within(pairing).getByLabelText('Code shown on the TV'), '12');
    await user.click(within(pairing).getByRole('button', { name: 'Pair' }));
    expect(await within(pairing).findByRole('alert')).toHaveTextContent('The code is 6 characters');
    expect(within(pairing).queryByText('Dining room TV is paired.')).not.toBeInTheDocument();
  });

  it('explains how to set up the phone, with the report URL, token and bodies', async () => {
    const { user } = renderAt('/presence');
    const setup = await screen.findByRole('region', { name: 'Set up your phone' });
    expect(within(setup).getByText(`${window.location.origin}/api/devices/phone/report`)).toBeInTheDocument();
    expect(within(setup).getByText('mock-report-token')).toBeInTheDocument();
    expect(within(setup).getByText('Authorization: Bearer mock-report-token')).toBeInTheDocument();
    expect(within(setup).getByText(/Tasker, MacroDroid or Automate/)).toBeInTheDocument();
    expect(within(setup).getByText(/Install Tailscale on the phone and the Mac/)).toBeInTheDocument();

    const table = within(setup).getByRole('table', { name: 'What the phone sends for each event' });
    for (const [event, body] of [
      ['Left home', '{"home": false}'],
      ['Arrived home', '{"home": true}'],
      ['Started charging', '{"charging": true}'],
      ['Stopped charging', '{"charging": false}'],
      ['Went to sleep', '{"asleep": true}'],
      ['Woke up', '{"asleep": false}'],
    ] as const) {
      expect(within(table).getByRole('row', { name: `${event} ${body}` })).toBeInTheDocument();
    }

    await user.click(within(setup).getByRole('button', { name: 'Copy token' }));
    expect(await within(setup).findByText('Copied')).toBeInTheDocument();
    await expect(navigator.clipboard.readText()).resolves.toBe('mock-report-token');
  });

  it('asks you to open the page on the Mac when the server hides the token', async () => {
    renderAt('/', [{ path: '/', element: <PhoneSetup phoneDeviceId="phone" reportToken={null} /> }]);
    expect(await screen.findByText('Open this page on the Mac itself to see the token.')).toBeInTheDocument();
    expect(screen.getByText('Authorization: Bearer <token>')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Copy token' })).not.toBeInTheDocument();
  });
});
