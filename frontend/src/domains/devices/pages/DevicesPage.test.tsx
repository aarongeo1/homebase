import { fireEvent, screen, waitFor, within } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderAt } from '@/test/render';

const article = (name: string) => screen.findByRole('article', { name });

/** Tiles remount with the device the server returned, so wait on the page rather than on an old tile. */
const expectState = (name: string, state: string) =>
  waitFor(() => expect(within(screen.getByRole('article', { name })).getByText(state)).toBeInTheDocument());

describe('DevicesPage', () => {
  it('groups lights by room in the order the API sends rooms', async () => {
    renderAt('/devices');
    const living = await screen.findByRole('region', { name: 'Living room' });
    expect(within(living).getByRole('article', { name: 'Floor lamp' })).toBeInTheDocument();
    expect(within(living).getByRole('article', { name: 'TV backlight' })).toBeInTheDocument();
    expect(within(living).queryByRole('article', { name: 'Dining room TV' })).not.toBeInTheDocument();
    const bedroom = screen.getByRole('region', { name: 'Bedroom' });
    expect(within(bedroom).getByRole('article', { name: 'Bedside lamp' })).toBeInTheDocument();

    const headings = screen.getAllByRole('heading', { level: 2 }).map((h) => h.textContent);
    expect(headings.indexOf('Living room')).toBeLessThan(headings.indexOf('Bedroom'));
    expect(screen.queryByRole('region', { name: 'Not in a room' })).not.toBeInTheDocument();
  });

  it('turns a light off and shows the state the server confirmed', async () => {
    const { user } = renderAt('/devices');
    await user.click(within(await article('Floor lamp')).getByRole('button', { name: 'Turn off' }));
    await expectState('Floor lamp', 'Off');
  });

  it('sets brightness and warmth when a slider is let go', async () => {
    renderAt('/devices');
    const warmth = within(await article('Floor lamp')).getByRole('slider', { name: 'Warmth 2700 K' });
    fireEvent.change(warmth, { target: { value: '2200' } });
    fireEvent.pointerUp(warmth);
    await expectState('Floor lamp', 'On · 70% · 2200 K');

    const brightness = within(await article('Floor lamp')).getByRole('slider', { name: 'Brightness 70%' });
    fireEvent.change(brightness, { target: { value: '30' } });
    fireEvent.keyUp(brightness);
    await expectState('Floor lamp', 'On · 30% · 2200 K');
  });

  it('shows offline lights disabled, with the reason', async () => {
    renderAt('/devices');
    const strip = await article('TV backlight');
    expect(within(strip).getByText('Offline')).toBeInTheDocument();
    expect(within(strip).getByText(/can't reach it through the Govee cloud/)).toBeInTheDocument();
    expect(within(strip).getByRole('button', { name: 'Turn on' })).toBeDisabled();
    expect(within(strip).getByRole('slider', { name: /Brightness/ })).toBeDisabled();
    expect(within(strip).getByRole('slider', { name: /Warmth/ })).toBeDisabled();
  });

  it('shows the TV and phone read-only, and sends you to pair the TV', async () => {
    const { user } = renderAt('/devices');
    const watched = await screen.findByRole('region', { name: 'TV and phone' });
    expect(within(watched).queryAllByRole('button')).toHaveLength(0);
    expect(within(watched).queryAllByRole('slider')).toHaveLength(0);
    expect(within(within(watched).getByRole('article', { name: 'Phone' })).getByText('Home · Not charging · Awake')).toBeInTheDocument();

    const tv = within(watched).getByRole('article', { name: 'Dining room TV' });
    expect(within(tv).getByText('Not paired')).toBeInTheDocument();
    const link = within(tv).getByRole('link', { name: /Pair it on the Presence page/ });
    expect(link).toHaveAttribute('href', '/presence#pairing');
    await user.click(link);
    expect(await screen.findByRole('region', { name: 'Pair the TV' })).toBeInTheDocument();
  });

  it('finds new lights once and lists them under "Not in a room"', async () => {
    const { user } = renderAt('/devices');
    await user.click(await screen.findByRole('button', { name: 'Find lights' }));
    expect(await screen.findByText('Added: Desk lamp.')).toBeInTheDocument();
    expect(screen.getByText(/Found 1 new light on the local network/)).toBeInTheDocument();
    const unassigned = await screen.findByRole('region', { name: 'Not in a room' });
    expect(await within(unassigned).findByRole('article', { name: 'Desk lamp' })).toBeInTheDocument();

    await user.click(await screen.findByRole('button', { name: 'Find lights' }));
    expect(await screen.findByText('No new lights added.')).toBeInTheDocument();
    expect(screen.getByText(/No new lights found/)).toBeInTheDocument();
    expect(screen.getAllByRole('article', { name: 'Desk lamp' })).toHaveLength(1);
  });

  it('renames a light and moves it to another room', async () => {
    const { user } = renderAt('/devices');
    await user.click(within(await article('Bedside lamp')).getByRole('button', { name: 'Rename or move' }));
    const form = screen.getByRole('form', { name: 'Rename or move Bedside lamp' });
    await user.clear(within(form).getByLabelText('Name'));
    await user.type(within(form).getByLabelText('Name'), 'Reading lamp');
    await user.selectOptions(within(form).getByLabelText('Room'), 'Living room');
    await user.click(within(form).getByRole('button', { name: 'Save' }));

    const living = screen.getByRole('region', { name: 'Living room' });
    expect(await within(living).findByRole('article', { name: 'Reading lamp' })).toBeInTheDocument();
    expect(within(screen.getByRole('region', { name: 'Bedroom' })).getByText('No lights here yet.')).toBeInTheDocument();
  });

  it('moves a light out of every room', async () => {
    const { user } = renderAt('/devices');
    await user.click(within(await article('Floor lamp')).getByRole('button', { name: 'Rename or move' }));
    const form = screen.getByRole('form', { name: 'Rename or move Floor lamp' });
    await user.selectOptions(within(form).getByLabelText('Room'), 'Not in a room');
    await user.click(within(form).getByRole('button', { name: 'Save' }));
    const unassigned = await screen.findByRole('region', { name: 'Not in a room' });
    expect(within(unassigned).getByRole('article', { name: 'Floor lamp' })).toBeInTheDocument();
  });

  it('shows the server error when a rename is rejected, and keeps the form open', async () => {
    const { user } = renderAt('/devices');
    await user.click(within(await article('Floor lamp')).getByRole('button', { name: 'Rename or move' }));
    const form = screen.getByRole('form', { name: 'Rename or move Floor lamp' });
    await user.clear(within(form).getByLabelText('Name'));
    await user.click(within(form).getByRole('button', { name: 'Save' }));
    expect(await within(form).findByRole('alert')).toHaveTextContent('The name must be 1–100 characters.');
    expect(screen.getByRole('form', { name: 'Rename or move Floor lamp' })).toBeInTheDocument();
  });

  it('activates a scene and says which steps did not apply', async () => {
    const { user } = renderAt('/devices');
    await user.click(within(await screen.findByRole('group', { name: 'Scenes' })).getByRole('button', { name: 'Bedtime' }));
    expect(await screen.findByText('Bedtime: not everything worked. Living room lights → off (a light is offline).')).toBeInTheDocument();
    await expectState('Bedside lamp', 'On · 25% · 2200 K');
    await expectState('Floor lamp', 'Off');
  });
});
