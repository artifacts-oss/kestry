import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { Download, Snapshot } from '../api/types';
import { api, ApiError } from '../api/client';
import { ToastProvider } from '../components/ui/Toaster';
import { DownloadsPage } from './DownloadsPage';

vi.mock('../api/client', async (orig) => ({
  ...(await orig<typeof import('../api/client')>()),
  api: {
    pause: vi.fn().mockResolvedValue(undefined),
    resume: vi.fn().mockResolvedValue(undefined),
    retry: vi.fn().mockResolvedValue({ gid: 'new' }),
    remove: vi.fn().mockResolvedValue(undefined),
    pauseAll: vi.fn().mockResolvedValue(undefined),
    resumeAll: vi.fn().mockResolvedValue(undefined),
    previewLinks: vi.fn().mockResolvedValue({ links: [], valid: 0, duplicates: 0, invalid: 0 }),
    addLinks: vi.fn(),
    addTorrents: vi.fn(),
  },
}));

const d = (gid: string, status: Download['status'], extra: Partial<Download> = {}): Download => ({
  gid, name: `${gid}.bin`, path: `/d/${gid}.bin`, status, source: 'https', fileType: 'file', size: 100, downloaded: 50, speed: 10,
  uploadSpeed: 0, eta: 5, connections: 0, seeders: 0, fetchingMetadata: false, seeding: false, errorCode: 0, error: null, ...extra,
});

const snap = (downloads: Download[], aria2: Partial<Snapshot['aria2']> = {}): Snapshot => ({
  downloads,
  stats: { downloadSpeed: 2048, uploadSpeed: 0, freeDiskBytes: 5 * 1024 ** 3 },
  aria2: { state: 'running', reasonCode: null, reason: null, ...aria2 },
  shutdown: null,
  settingsVersion: 0,
});

const renderPage = (s: Snapshot | null, connected = true) =>
  render(<ToastProvider><DownloadsPage snapshot={s} connected={connected} aria2Version="1.37.0" /></ToastProvider>);

const all = [d('a', 'downloading'), d('b', 'paused'), d('c', 'completed'), d('e', 'failed', { error: 'No space' })];

beforeEach(() => vi.clearAllMocks());

describe('DownloadsPage', () => {
  it('shows the empty state when there are no downloads', () => {
    renderPage(snap([]));
    expect(screen.getByRole('heading', { name: 'Aún no hay descargas' })).toBeInTheDocument();
  });

  it('lists downloads with a summary and filter counts', () => {
    renderPage(snap(all));
    expect(screen.getByText('1 en curso · 1 en cola')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /^Todas\s*4$/ })).toHaveAttribute('aria-current', 'page');
    expect(screen.getByRole('button', { name: /^En cola\s*1$/ })).toBeInTheDocument();
    expect(screen.getAllByRole('listitem')).toHaveLength(4);
  });

  it('filters by status and by search text', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: /^Completadas/ }));
    expect(screen.getAllByRole('listitem')).toHaveLength(1);
    expect(screen.getByRole('button', { name: /^Completadas/ })).toHaveAttribute('aria-current', 'page');

    await userEvent.click(screen.getByRole('button', { name: /^Todas/ }));
    await userEvent.type(screen.getByRole('textbox', { name: 'Buscar por nombre' }), 'zzz');
    expect(screen.getByText('Ninguna descarga coincide con el filtro')).toBeInTheDocument();
  });

  it('pauses a download through the API', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Pausar a.bin' }));
    expect(api.pause).toHaveBeenCalledWith('a');
  });

  it('pauses and resumes everything', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Pausar todo' }));
    await userEvent.click(screen.getByRole('button', { name: 'Reanudar todo' }));
    expect(api.pauseAll).toHaveBeenCalled();
    expect(api.resumeAll).toHaveBeenCalled();
  });

  it('asks for confirmation before cancelling, then deletes partial files', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar a.bin' }));
    expect(api.remove).not.toHaveBeenCalled();
    const dialog = screen.getByRole('dialog', { name: 'Cancelar descarga' });
    await userEvent.click(within(dialog).getByRole('button', { name: 'Cancelar descarga' }));
    expect(api.remove).toHaveBeenCalledWith('a', true);
  });

  it('keeps the files when removing a finished download unless asked', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Eliminar c.bin' }));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Eliminar' }));
    expect(api.remove).toHaveBeenCalledWith('c', false);

    await userEvent.click(screen.getByRole('button', { name: 'Eliminar c.bin' }));
    await userEvent.click(screen.getByLabelText('Borrar también los archivos'));
    await userEvent.click(within(screen.getByRole('dialog')).getByRole('button', { name: 'Eliminar' }));
    expect(api.remove).toHaveBeenLastCalledWith('c', true);
  });

  it('closing the confirmation does nothing', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar a.bin' }));
    await userEvent.click(screen.getByRole('button', { name: 'Volver' }));
    expect(api.remove).not.toHaveBeenCalled();
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('retries a failed download', async () => {
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Reintentar e.bin' }));
    expect(api.retry).toHaveBeenCalledWith('e');
  });

  it('copies the path of a completed download', async () => {
    const writeText = vi.fn().mockResolvedValue(undefined);
    Object.defineProperty(navigator, 'clipboard', { value: { writeText }, configurable: true });
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Copiar ruta c.bin' }));
    expect(writeText).toHaveBeenCalledWith('/d/c.bin');
    expect(await screen.findByText('Ruta copiada')).toBeInTheDocument();
  });

  it('shows an error toast when an action fails', async () => {
    vi.mocked(api.pause).mockRejectedValueOnce(new ApiError('GID is not found', 404, 'downloads.notFound'));
    renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Pausar a.bin' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('La descarga ya no existe.');
  });

  it('announces a download that completes between snapshots', () => {
    const { rerender } = renderPage(snap([d('a', 'downloading')]));
    rerender(<ToastProvider><DownloadsPage snapshot={snap([d('a', 'completed')])} connected aria2Version="1.37.0" /></ToastProvider>);
    // The provider is re-created by rerender, so the toast lands in the new tree.
    expect(screen.getByText('Descarga completada: a.bin')).toBeInTheDocument();
  });
});

const paste = (text: string, target: Element = document.body) => {
  const event = new Event('paste', { bubbles: true, cancelable: true });
  Object.defineProperty(event, 'clipboardData', { value: { getData: () => text } });
  target.dispatchEvent(event);
  return event;
};

describe('failed downloads', () => {
  it('translates the aria2 error code and falls back to its message for unknown codes', () => {
    renderPage(snap([d('a', 'failed', { errorCode: 9, error: 'File allocation failed' }), d('b', 'failed', { errorCode: 99, error: 'Weird one' })]));
    expect(screen.getByText(/No hay espacio suficiente en disco/)).toBeInTheDocument();
    expect(screen.queryByText(/File allocation failed/)).toBeNull();
    expect(screen.getByText(/Weird one/)).toBeInTheDocument();
  });
});

describe('adding downloads', () => {
  it('opens the dialog from the toolbar and from the empty state', async () => {
    const { unmount } = renderPage(snap(all));
    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));
    expect(screen.getByRole('dialog', { name: 'Añadir descargas' })).toBeInTheDocument();
    unmount();

    renderPage(snap([]));
    await userEvent.click(screen.getByRole('button', { name: 'Pega tus enlaces aquí' }));
    expect(screen.getByRole('dialog', { name: 'Añadir descargas' })).toBeInTheDocument();
  });

  it('pasting outside a text field opens the dialog with the text', async () => {
    renderPage(snap(all));
    act(() => { paste('https://a.com/file.zip'); });
    expect(await screen.findByLabelText('Enlaces')).toHaveValue('https://a.com/file.zip');
  });

  it('pasting into the search box does not open the dialog', () => {
    renderPage(snap(all));
    paste('https://a.com/file.zip', screen.getByRole('textbox', { name: 'Buscar por nombre' }));
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('pasting empty text does nothing', () => {
    renderPage(snap(all));
    paste('   ');
    expect(screen.queryByRole('dialog')).toBeNull();
  });
});

describe('connection indicator', () => {
  it.each([
    [snap([]), true, 'aria2 1.37.0 conectado'],
    [snap([], { state: 'installing' }), true, 'Instalando aria2…'],
    [snap([], { state: 'restarting' }), true, 'Reconectando con aria2…'],
    [snap([], { state: 'down', reasonCode: 'notFound' }), true, 'aria2 no responde'],
    [null, false, 'Sin conexión con Kestry'],
  ])('shows the right state', (s, connected, text) => {
    renderPage(s, connected);
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('explains why aria2 is down', () => {
    renderPage(snap([], { state: 'down', reasonCode: 'notFound' }));
    expect(screen.getByText(/Instala aria2/)).toBeInTheDocument();
  });

  it('shows speed and free disk in the sidebar', () => {
    renderPage(snap([]));
    expect(screen.getByText('2.0')).toBeInTheDocument();
    expect(screen.getByText('KB/s')).toBeInTheDocument();
    expect(screen.getByText('5.0')).toBeInTheDocument();
  });
});
