import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { getLocale, setLocale } from '../i18n';
import { localeStorageKey } from '../i18n/config';
import { Header } from './Header';
import { LocaleSwitcher } from './LocaleSwitcher';

describe('LocaleSwitcher', () => {
  it('lists every language, marks the current one and switches without a reload', async () => {
    render(<><LocaleSwitcher /><Header settings={null} shutdownAvailable onToggle={() => {}} onOpenSettings={() => {}} /></>);
    await userEvent.click(screen.getAllByRole('button', { name: 'Cambiar idioma' })[0]);

    const options = await screen.findAllByRole('menuitemradio');
    expect(options.map((o) => o.textContent)).toEqual(['Inglés', 'Español', 'Alemán', 'Francés']);
    expect(screen.getByRole('menuitemradio', { name: 'Español' })).toBeChecked();

    await userEvent.click(screen.getByRole('menuitemradio', { name: 'Alemán' }));

    expect(getLocale()).toBe('de');
    expect(localStorage.getItem(localeStorageKey)).toBe('de');
    expect(document.documentElement.lang).toBe('de');
    // Everything already on screen is re-translated in place.
    expect(screen.getAllByRole('button', { name: 'Sprache wechseln' }).length).toBeGreaterThan(0);
    expect(screen.getByRole('button', { name: 'Einstellungen' })).toBeInTheDocument();
  });

  it('switches back to English', async () => {
    setLocale('fr');
    render(<LocaleSwitcher />);
    await userEvent.click(screen.getByRole('button', { name: 'Changer de langue' }));
    await userEvent.click(await screen.findByRole('menuitemradio', { name: 'Anglais' }));
    expect(screen.getByRole('button', { name: 'Switch language' })).toBeInTheDocument();
  });
});
