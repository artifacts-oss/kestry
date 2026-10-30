import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { DownloadItem, type DownloadItemProps } from './DownloadItem';
import { FileDrop } from './FileDrop';
import { Banner } from './layout';
import { EmptyState, NavItem, SpeedStat, Toolbar } from './layout';
import { formatBytes, formatEta } from './format';
import { ProgressBar, StatusBadge } from './status';

describe('format', () => {
  it('formats bytes', () => {
    expect(formatBytes(0)).toBe('0 B');
    expect(formatBytes(1536)).toBe('1.5 KB');
    expect(formatBytes(150 * 1024 * 1024)).toBe('150 MB');
    expect(formatBytes(undefined)).toBe('—');
  });

  it.each([
    [30, 'quedan 30 s'],
    [90, 'quedan 2 min'],
    [3 * 3600 + 25 * 60, 'quedan 3 h 25 min'],
  ])('formats ETA %s', (seconds, text) => expect(formatEta(seconds)).toBe(text));

  it('returns null for unknown ETA', () => {
    expect(formatEta(undefined)).toBeNull();
    expect(formatEta(Infinity)).toBeNull();
  });
});

describe('StatusBadge / ProgressBar', () => {
  it.each([
    ['downloading', 'Descargando'],
    ['queued', 'En cola'],
    ['paused', 'Pausada'],
    ['completed', 'Completada'],
    ['failed', 'Error'],
  ] as const)('%s badge carries a word', (status, text) => {
    render(<StatusBadge status={status} />);
    expect(screen.getByText(text)).toBeInTheDocument();
  });

  it('exposes progress value, clamped', () => {
    render(<ProgressBar value={140} label="p" />);
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '100');
  });

  it('omits aria-valuenow when indeterminate', () => {
    render(<ProgressBar label="p" />);
    expect(screen.getByRole('progressbar')).not.toHaveAttribute('aria-valuenow');
  });
});

describe('DownloadItem', () => {
  const base: DownloadItemProps = { name: 'ubuntu.iso', status: 'downloading', size: 1000, downloaded: 250, speed: 2048, eta: 120 };

  it('shows progress, speed and ETA', () => {
    render(<DownloadItem {...base} source="https" />);
    expect(screen.getByRole('progressbar')).toHaveAttribute('aria-valuenow', '25');
    expect(screen.getByText(/HTTPS · 250 B de 1000 B · ↓ 2\.0 KB\/s · quedan 2 min/)).toBeInTheDocument();
  });

  it('shows upload speed and peers only for torrents', () => {
    render(<DownloadItem {...base} source="torrent" uploadSpeed={1024} connections={5} seeders={2} />);
    expect(screen.getByText(/↑ 1\.0 KB\/s/)).toBeInTheDocument();
    expect(screen.getByText(/5 conexiones · 2 seeds/)).toBeInTheDocument();
  });

  it('shows an indeterminate bar while fetching magnet metadata', () => {
    render(<DownloadItem name="m" status="downloading" source="magnet" fetchingMetadata />);
    expect(screen.getByRole('progressbar')).not.toHaveAttribute('aria-valuenow');
    expect(screen.getByText(/Obteniendo metadatos…/)).toBeInTheDocument();
  });

  it('downloading → pause and cancel', async () => {
    const onPause = vi.fn();
    const onCancel = vi.fn();
    render(<DownloadItem {...base} onPause={onPause} onCancel={onCancel} />);
    await userEvent.click(screen.getByRole('button', { name: 'Pausar ubuntu.iso' }));
    await userEvent.click(screen.getByRole('button', { name: 'Cancelar ubuntu.iso' }));
    expect(onPause).toHaveBeenCalledOnce();
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it.each([
    ['paused', ['Reanudar', 'Cancelar']],
    ['queued', ['Cancelar']],
    ['failed', ['Reintentar', 'Eliminar']],
    ['completed', ['Copiar ruta', 'Eliminar']],
  ] as const)('%s actions', (status, labels) => {
    render(<DownloadItem name="f" status={status} size={10} />);
    const found = screen.getAllByRole('button').map((b) => b.getAttribute('aria-label')?.replace(' f', ''));
    expect(found).toEqual(labels);
  });

  it('hides the progress bar when completed and shows the error when failed', () => {
    const { rerender } = render(<DownloadItem name="f" status="completed" size={10} />);
    expect(screen.queryByRole('progressbar')).toBeNull();
    rerender(<DownloadItem name="f" status="failed" error="Sin espacio" />);
    expect(screen.getByText('Sin espacio')).toBeInTheDocument();
  });

  it('puts the path in the title', () => {
    render(<DownloadItem name="f" path="/downloads/f" status="queued" />);
    expect(screen.getByTitle('/downloads/f')).toBeInTheDocument();
  });
});

describe('layout', () => {
  it('NavItem marks the active filter with aria-current and shows the count', () => {
    render(<NavItem label="Todas" count={3} active />);
    const btn = screen.getByRole('button', { name: /Todas/ });
    expect(btn).toHaveAttribute('aria-current', 'page');
    expect(btn).toHaveTextContent('3');
  });

  it('Toolbar is a labelled toolbar', () => {
    render(<Toolbar label="Descargas"><Toolbar.Spacer /></Toolbar>);
    expect(screen.getByRole('toolbar', { name: 'Descargas' })).toBeInTheDocument();
  });

  it('SpeedStat shows value and unit', () => {
    render(<SpeedStat label="Descarga" value="2.4" unit="MB/s" />);
    expect(screen.getByText('Descarga')).toBeInTheDocument();
    expect(screen.getByText('MB/s')).toBeInTheDocument();
  });

  it('EmptyState renders title, description and action', () => {
    render(<EmptyState title="Sin descargas" description="Pega enlaces" action={<button>Ir</button>} />);
    expect(screen.getByRole('heading', { name: 'Sin descargas' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Ir' })).toBeInTheDocument();
  });

  it('Banner uses an alert role for action tone', () => {
    render(<Banner tone="action">Apagando</Banner>);
    expect(screen.getByRole('alert')).toHaveTextContent('Apagando');
  });
});

describe('FileDrop', () => {
  it('keeps only files with an accepted extension', async () => {
    const onFiles = vi.fn();
    render(<FileDrop accept=".torrent" onFiles={onFiles} />);
    const good = new File(['x'], 'a.torrent');
    const bad = new File(['x'], 'b.txt');
    await userEvent.upload(screen.getByLabelText(/Suelta archivos/, { selector: 'input' }), [good, bad], { applyAccept: false });
    expect(onFiles).toHaveBeenCalledWith([good]);
    expect(screen.getByText('1 archivo seleccionado')).toBeInTheDocument();
  });
});
