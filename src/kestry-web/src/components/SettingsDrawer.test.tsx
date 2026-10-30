import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { AppSettings, SystemInfo } from '../api/types';
import { SettingsDrawer } from './SettingsDrawer';
import { Header } from './Header';
import { ShutdownBanner } from './ShutdownBanner';

const settings: AppSettings = {
  maxConcurrentDownloads: 3, speedLimitEnabled: false, downloadLimitKBps: 5120, uploadLimitKBps: 512,
  downloadDir: '/downloads', shutdownWhenDone: false, seedingEnabled: false, seedRatio: 1,
};
const system: SystemInfo = { version: '1.0.0', aria2Version: '1.37.0', shutdownAvailable: true, shutdownUnavailableReason: null };

const setup = (over: Partial<AppSettings> = {}, sys: SystemInfo = system, save = vi.fn().mockResolvedValue({})) => {
  render(<SettingsDrawer open onClose={() => {}} settings={{ ...settings, ...over }} system={sys} onSave={save} />);
  return save;
};

beforeEach(() => {
  localStorage.clear();
  document.documentElement.classList.remove('dark');
});

describe('SettingsDrawer', () => {
  it('shows the current values, picking MB/s for whole megabytes', () => {
    setup();
    expect(screen.getByLabelText('Descargas simultáneas')).toHaveValue(3);
    expect(screen.getByLabelText('Límite de descarga')).toHaveValue(5);
    expect(screen.getByLabelText('Límite de subida (torrents)')).toHaveValue(512);
    expect(screen.getAllByLabelText('Unidad').map((s) => (s as HTMLSelectElement).value)).toEqual(['MB', 'KB']);
    expect(screen.getByLabelText('Carpeta de descargas')).toHaveValue('/downloads');
  });

  it('saves a switch immediately with the rest of the settings', async () => {
    const save = setup();
    await userEvent.click(screen.getByRole('switch', { name: 'Límite de velocidad activo' }));
    expect(save).toHaveBeenCalledWith({ ...settings, speedLimitEnabled: true });
  });

  it('saves text fields on blur, and only when they changed', async () => {
    const save = setup();
    const field = screen.getByLabelText('Descargas simultáneas');

    await userEvent.click(field);
    await userEvent.tab();
    expect(save).not.toHaveBeenCalled();

    await userEvent.clear(field);
    await userEvent.type(field, '8');
    expect(save).not.toHaveBeenCalled();
    await userEvent.tab();
    expect(save).toHaveBeenCalledWith({ ...settings, maxConcurrentDownloads: 8 });
  });

  it('converts the limit to KB/s using the selected unit', async () => {
    const save = setup();
    await userEvent.selectOptions(screen.getAllByLabelText('Unidad')[0], 'KB');
    expect(save).toHaveBeenCalledWith({ ...settings, downloadLimitKBps: 5 });

    save.mockClear();
    const upload = screen.getByLabelText('Límite de subida (torrents)');
    await userEvent.clear(upload);
    await userEvent.type(upload, '2');
    await userEvent.selectOptions(screen.getAllByLabelText('Unidad')[1], 'MB');
    expect(save).toHaveBeenLastCalledWith(expect.objectContaining({ uploadLimitKBps: 2048 }));
  });

  it('saves the folder trimmed and shows server errors in Spanish next to the field', async () => {
    const save = vi.fn().mockResolvedValue({ downloadDir: 'Must be inside one of the allowed folders.' });
    setup({}, system, save);
    const dir = screen.getByLabelText('Carpeta de descargas');

    await userEvent.clear(dir);
    await userEvent.type(dir, '  /etc  ');
    await userEvent.tab();

    expect(save).toHaveBeenCalledWith({ ...settings, downloadDir: '/etc' });
    expect(await screen.findByText(/ruta absoluta dentro de las carpetas permitidas/)).toBeInTheDocument();
    expect(dir).toHaveAttribute('aria-invalid', 'true');
    expect(dir).toHaveValue('  /etc  '); // the draft is kept as typed so the user can fix it
  });

  it('only shows the ratio when seeding is on', async () => {
    const { rerender } = render(<SettingsDrawer open onClose={() => {}} settings={settings} system={system} onSave={vi.fn()} />);
    expect(screen.queryByLabelText('Ratio de compartición')).toBeNull();
    rerender(<SettingsDrawer open onClose={() => {}} settings={{ ...settings, seedingEnabled: true }} system={system} onSave={vi.fn()} />);
    expect(screen.getByLabelText('Ratio de compartición')).toHaveValue(1);
  });

  it('disables shutdown with an explanation inside a container', () => {
    setup({}, { ...system, shutdownAvailable: false, shutdownUnavailableReason: 'container' });
    expect(screen.getByRole('switch', { name: 'Apagar al terminar todo' })).toBeDisabled();
    expect(screen.getByText(/contenedor/)).toBeInTheDocument();
  });

  it('lets an already-enabled shutdown be turned off even if it became unavailable', async () => {
    const save = setup({ shutdownWhenDone: true }, { ...system, shutdownAvailable: false, shutdownUnavailableReason: 'disabled' });
    await userEvent.click(screen.getByRole('switch', { name: 'Apagar al terminar todo' }));
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ shutdownWhenDone: false }));
  });

  it('switches the theme in the browser only', async () => {
    const save = setup();
    await userEvent.click(screen.getByText('Oscuro'));
    expect(document.documentElement).toHaveClass('dark');
    expect(localStorage.getItem('kestry.theme')).toBe('dark');
    expect(save).not.toHaveBeenCalled();
  });

  it('renders an empty drawer while settings are loading', () => {
    render(<SettingsDrawer open onClose={() => {}} settings={null} system={null} onSave={vi.fn()} />);
    expect(screen.getByRole('dialog', { name: 'Ajustes' })).toBeInTheDocument();
  });
});

describe('Header', () => {
  it('toggles the quick switches and opens the settings', async () => {
    const onToggle = vi.fn();
    const onOpenSettings = vi.fn();
    render(<Header settings={settings} shutdownAvailable onToggle={onToggle} onOpenSettings={onOpenSettings} />);

    await userEvent.click(screen.getByRole('switch', { name: 'Límite de velocidad' }));
    await userEvent.click(screen.getByRole('switch', { name: 'Apagar al terminar' }));
    await userEvent.click(screen.getByRole('button', { name: 'Ajustes' }));

    expect(onToggle).toHaveBeenNthCalledWith(1, { speedLimitEnabled: true });
    expect(onToggle).toHaveBeenNthCalledWith(2, { shutdownWhenDone: true });
    expect(onOpenSettings).toHaveBeenCalled();
  });

  it('disables the shutdown switch when unavailable', () => {
    render(<Header settings={settings} shutdownAvailable={false} onToggle={() => {}} onOpenSettings={() => {}} />);
    expect(screen.getByRole('switch', { name: 'Apagar al terminar' })).toBeDisabled();
  });

  it('still shows the settings button before settings load', () => {
    render(<Header settings={null} shutdownAvailable onToggle={() => {}} onOpenSettings={() => {}} />);
    expect(screen.queryAllByRole('switch')).toHaveLength(0);
    expect(screen.getByRole('button', { name: 'Ajustes' })).toBeInTheDocument();
  });
});

describe('ShutdownBanner', () => {
  it('shows the countdown and cancels', async () => {
    const onCancel = vi.fn();
    render(<ShutdownBanner seconds={42} onCancel={onCancel} />);
    expect(screen.getByRole('alert')).toHaveTextContent('El equipo se apagará en 42 s');
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar apagado' }));
    expect(onCancel).toHaveBeenCalled();
  });

  it('renders nothing without a countdown', () => {
    const { container } = render(<ShutdownBanner seconds={null} onCancel={() => {}} />);
    expect(container).toBeEmptyDOMElement();
  });
});
