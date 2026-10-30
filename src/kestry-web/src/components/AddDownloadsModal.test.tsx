import { act, render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { api, ApiError } from '../api/client';
import type { LinksResponse } from '../api/types';
import { AddDownloadsModal, PREVIEW_DELAY_MS } from './AddDownloadsModal';
import { ToastProvider } from './ui/Toaster';

vi.mock('../api/client', async (orig) => ({
  ...(await orig<typeof import('../api/client')>()),
  api: { previewLinks: vi.fn(), addLinks: vi.fn(), addTorrents: vi.fn() },
}));

const response = (valid: number, duplicates = 0, invalid: string[] = []): LinksResponse => ({
  links: invalid.map((text) => ({ text, kind: 'invalid', state: 'invalid' })),
  valid,
  duplicates,
  invalid: invalid.length,
});

const setup = (initialText = '') => {
  const onClose = vi.fn();
  render(<ToastProvider><AddDownloadsModal open initialText={initialText} onClose={onClose} /></ToastProvider>);
  return { onClose, links: screen.getByLabelText('Enlaces') };
};

beforeEach(() => {
  vi.clearAllMocks();
  vi.mocked(api.previewLinks).mockResolvedValue(response(2, 1, ['basura']));
  vi.mocked(api.addLinks).mockResolvedValue(response(2));
  vi.mocked(api.addTorrents).mockResolvedValue([{ name: 'a.torrent', status: 'added', gid: 'g' }]);
});

describe('AddDownloadsModal', () => {
  it('starts with the pasted text and disables Añadir until something is valid', () => {
    setup('https://a.com/f');
    expect(screen.getByLabelText('Enlaces')).toHaveValue('https://a.com/f');
    expect(screen.getByRole('button', { name: 'Añadir' })).toBeDisabled();
  });

  it('debounces the preview and summarizes valid, duplicate and invalid links', async () => {
    vi.useFakeTimers({ shouldAdvanceTime: true });
    try {
      const { links } = setup();
      await userEvent.type(links, 'abc');
      expect(api.previewLinks).not.toHaveBeenCalled();

      await act(async () => { vi.advanceTimersByTime(PREVIEW_DELAY_MS + 10); });

      expect(api.previewLinks).toHaveBeenCalledTimes(1);
      expect(api.previewLinks).toHaveBeenCalledWith('abc');
      expect(await screen.findByText('2 válidos')).toBeInTheDocument();
      expect(screen.getByText('1 duplicado')).toBeInTheDocument();
      expect(screen.getByText('1 inválido')).toBeInTheDocument();
      expect(screen.getByText('basura')).toBeInTheDocument();
      expect(screen.getByRole('button', { name: 'Añadir' })).toBeEnabled();
    } finally {
      vi.useRealTimers();
    }
  });

  it('adds the pasted links with the advanced options and closes', async () => {
    const { links, onClose } = setup();
    await userEvent.type(links, 'x');
    await screen.findByText('2 válidos');
    await userEvent.click(screen.getByText('Opciones avanzadas'));
    await userEvent.type(screen.getByLabelText('Subcarpeta de destino'), ' pelis ');
    await userEvent.click(screen.getByLabelText('Añadir en pausa'));

    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    expect(api.addLinks).toHaveBeenCalledWith({ text: 'x', subfolder: 'pelis', paused: true });
    expect(await screen.findByText('2 descargas añadidas')).toBeInTheDocument();
    expect(onClose).toHaveBeenCalled();
  });

  it('uploads dropped .torrent files without any link', async () => {
    setup();
    const file = new File(['d'], 'a.torrent');
    await userEvent.upload(screen.getByLabelText(/Suelta archivos/, { selector: 'input' }), file, { applyAccept: false });

    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    expect(api.addLinks).not.toHaveBeenCalled();
    expect(api.addTorrents).toHaveBeenCalledWith([file], undefined, undefined);
    expect(await screen.findByText('1 descarga añadida')).toBeInTheDocument();
  });

  it('shows the server error and stays open when adding fails', async () => {
    vi.mocked(api.addLinks).mockRejectedValue(new ApiError('bad', 400, 'downloads.invalidSubfolder'));
    const { links, onClose } = setup();
    await userEvent.type(links, 'x');
    await screen.findByText('2 válidos');

    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('La subcarpeta debe ser relativa y quedar dentro de la carpeta de descargas.');
    expect(onClose).not.toHaveBeenCalled();
    expect(screen.getByLabelText('Enlaces')).toHaveValue('x');
  });

  it('explains why a .torrent was rejected, translated, and still closes', async () => {
    vi.mocked(api.addTorrents).mockResolvedValue([
      { name: 'a.torrent', status: 'invalid', error: { code: 'torrents.tooLarge', error: 'x', params: { maxMb: 2 } } },
    ]);
    const { onClose } = setup();
    await userEvent.upload(screen.getByLabelText(/Suelta archivos/, { selector: 'input' }), new File(['d'], 'a.torrent'), { applyAccept: false });

    await userEvent.click(screen.getByRole('button', { name: 'Añadir' }));

    expect(await screen.findByRole('alert')).toHaveTextContent('Solo se aceptan archivos .torrent de hasta 2 MB.');
    expect(screen.queryByText('0 descargas añadidas')).toBeNull();
    expect(onClose).toHaveBeenCalled();
  });

  it('ignores a stale preview that arrives after the text changed', async () => {
    let resolveFirst!: (r: LinksResponse) => void;
    vi.mocked(api.previewLinks)
      .mockImplementationOnce(() => new Promise((r) => { resolveFirst = r; }))
      .mockResolvedValueOnce(response(5));
    const { links } = setup();
    await userEvent.type(links, 'a');
    await waitFor(() => expect(api.previewLinks).toHaveBeenCalledTimes(1));
    await userEvent.type(links, 'b');
    expect(await screen.findByText('5 válidos')).toBeInTheDocument();

    await act(async () => resolveFirst(response(1)));

    expect(screen.getByText('5 válidos')).toBeInTheDocument();
  });

  it('clears everything on cancel', async () => {
    const { links, onClose } = setup();
    await userEvent.type(links, 'x');
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar' }));
    expect(onClose).toHaveBeenCalled();
    expect(screen.getByLabelText('Enlaces')).toHaveValue('');
  });
});
