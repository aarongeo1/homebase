import { screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { renderAt } from '@/test/render';
import { domainModules } from './domain-registry';

describe('app routes', () => {
  it('lists every registered domain on the home page and in the nav', async () => {
    renderAt('/');
    for (const m of domainModules) {
      expect(await screen.findByRole('heading', { name: m.title })).toBeInTheDocument();
    }
    expect(screen.getByRole('navigation', { name: 'Areas' })).toBeInTheDocument();
  });

  it('has exactly three areas: devices, automations and presence', () => {
    expect(domainModules.map((m) => m.basePath)).toEqual(['devices', 'automations', 'presence']);
  });

  it('opens the presence page from the nav', async () => {
    const { user } = renderAt('/');
    await user.click(await screen.findByRole('link', { name: 'Home & away' }));
    expect(await screen.findByRole('heading', { level: 1, name: 'Presence' })).toBeInTheDocument();
  });

  it.each(['/energy', '/security'])('no longer serves %s', async (path) => {
    renderAt(path);
    expect(await screen.findByText('That page does not exist.')).toBeInTheDocument();
  });

  it('shows a not-found page for unknown URLs', async () => {
    renderAt('/nope');
    expect(await screen.findByText('That page does not exist.')).toBeInTheDocument();
  });
});
