import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api } from './api/client';
import type { AppSettings, Snapshot } from './api/types';
import { STALE_MS } from './api/useSnapshot';
import { App } from './App';

vi.mock('./api/client', async (orig) => ({
  ...(await orig<typeof import('./api/client')>()),
  api: {
    system: vi.fn(), getSettings: vi.fn(), saveSettings: vi.fn(), cancelShutdown: vi.fn(),
    pause: vi.fn(), resume: vi.fn(), retry: vi.fn(), remove: vi.fn(), pauseAll: vi.fn(), resumeAll: vi.fn(),
    previewLinks: vi.fn(), addLinks: vi.fn(), addTorrents: vi.fn(),
  },
}));

class FakeEventSource {
  static last: FakeEventSource;
  listeners: Record<string, (e: MessageEvent<string>) => void> = {};
  onopen: (() => void) | null = null;
  onerror: (() => void) | null = null;
  closed = false;
  constructor(public url: string) { FakeEventSource.last = this; }
  addEventListener(type: string, fn: (e: MessageEvent<string>) => void) { this.listeners[type] = fn; }
  close() { this.closed = true; }
  emit(snapshot: Snapshot) { this.listeners.snapshot({ data: JSON.stringify(snapshot) } as MessageEvent<string>); }
}

const settings: AppSettings = {
  maxConcurrentDownloads: 3, speedLimitEnabled: false, downloadLimitKBps: 5120, uploadLimitKBps: 1024,
  downloadDir: '/downloads', shutdownWhenDone: false, seedingEnabled: false, seedRatio: 1,
};
const snapshot = (over: Partial<Snapshot> = {}): Snapshot => ({
  downloads: [], stats: { downloadSpeed: 0, uploadSpeed: 0, freeDiskBytes: 1e9 },
  aria2: { state: 'running', reasonCode: null, reason: null }, shutdown: null, settingsVersion: 0, ...over,
});

beforeEach(() => {
  vi.clearAllMocks();
  vi.stubGlobal('EventSource', FakeEventSource);
  vi.mocked(api.getSettings).mockResolvedValue(settings);
  vi.mocked(api.saveSettings).mockImplementation(async (s) => s);
  vi.mocked(api.system).mockResolvedValue({ version: '1.0.0', aria2Version: '1.37.0', shutdownAvailable: true, shutdownUnavailableReason: null });
  vi.mocked(api.cancelShutdown).mockResolvedValue(undefined);
});
afterEach(() => vi.unstubAllGlobals());

describe('App', () => {
  it('connects to the event stream and renders what it sends', async () => {
    render(<App />);
    expect(FakeEventSource.last.url).toBe('/api/events');

    act(() => FakeEventSource.last.emit(snapshot()));

    expect(await screen.findByText('aria2 1.37.0 conectado')).toBeInTheDocument();
  });

  it('closes the event stream on unmount', () => {
    const { unmount } = render(<App />);
    const source = FakeEventSource.last;
    unmount();
    expect(source.closed).toBe(true);
  });

  it('reopens the event stream when it goes quiet', () => {
    vi.useFakeTimers();
    try {
      render(<App />);
      const first = FakeEventSource.last;
      act(() => first.emit(snapshot()));
      act(() => vi.advanceTimersByTime(STALE_MS - 1000));
      expect(FakeEventSource.last).toBe(first);

      act(() => vi.advanceTimersByTime(2000));
      expect(first.closed).toBe(true);
      expect(FakeEventSource.last).not.toBe(first);
      expect(FakeEventSource.last.url).toBe('/api/events');
    } finally {
      vi.useRealTimers();
    }
  });

  it('the header speed switch saves the settings', async () => {
    render(<App />);
    act(() => FakeEventSource.last.emit(snapshot()));
    await userEvent.click(await screen.findByRole('switch', { name: 'Límite de velocidad' }));
    expect(api.saveSettings).toHaveBeenCalledWith({ ...settings, speedLimitEnabled: true });
    await waitFor(() => expect(screen.getByRole('switch', { name: 'Límite de velocidad' })).toBeChecked());
  });

  it('reloads settings when another client saves (settingsVersion changes)', async () => {
    render(<App />);
    act(() => FakeEventSource.last.emit(snapshot()));
    await waitFor(() => expect(api.getSettings).toHaveBeenCalledTimes(1));
    vi.mocked(api.getSettings).mockResolvedValue({ ...settings, speedLimitEnabled: true });

    act(() => FakeEventSource.last.emit(snapshot({ settingsVersion: 1 })));

    await waitFor(() => expect(screen.getByRole('switch', { name: 'Límite de velocidad' })).toBeChecked());
  });

  it('opens the settings drawer from the header', async () => {
    render(<App />);
    act(() => FakeEventSource.last.emit(snapshot()));
    await userEvent.click(screen.getByRole('button', { name: 'Ajustes' }));
    expect(await screen.findByRole('dialog', { name: 'Ajustes' })).toBeInTheDocument();
    expect(await screen.findByLabelText('Descargas simultáneas')).toBeInTheDocument();
  });

  it('shows an error toast when saving fails unexpectedly', async () => {
    vi.mocked(api.saveSettings).mockRejectedValue(new Error('network'));
    render(<App />);
    act(() => FakeEventSource.last.emit(snapshot()));
    await userEvent.click(await screen.findByRole('switch', { name: 'Límite de velocidad' }));
    expect(await screen.findByRole('alert')).toHaveTextContent('No se pudieron guardar los ajustes');
  });

  it('shows the shutdown countdown and cancels it', async () => {
    render(<App />);
    act(() => FakeEventSource.last.emit(snapshot({ shutdown: 30 })));
    expect(await screen.findByText('El equipo se apagará en 30 s')).toBeInTheDocument();

    await userEvent.click(screen.getByRole('button', { name: 'Cancelar apagado' }));

    expect(api.cancelShutdown).toHaveBeenCalled();
  });
});
