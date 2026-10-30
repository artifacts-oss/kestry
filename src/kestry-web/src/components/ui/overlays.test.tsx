import { act, render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ActionMenu } from './ActionMenu';
import { Dialog, Drawer } from './overlays';
import { ToastProvider, TOAST_TIMEOUT_MS, useToast } from './Toaster';
import { DownloadItem } from './DownloadItem';

describe('Dialog / Drawer', () => {
  it.each([['Dialog', Dialog], ['Drawer', Drawer]])('%s renders title, content and footer, and closes on Escape', async (_n, C) => {
    const onClose = vi.fn();
    render(<C open title="Añadir descargas" onClose={onClose} footer={<button>Añadir</button>}>contenido</C>);
    const dialog = screen.getByRole('dialog', { name: 'Añadir descargas' });
    expect(within(dialog).getByText('contenido')).toBeInTheDocument();
    expect(within(dialog).getByRole('button', { name: 'Añadir' })).toBeInTheDocument();
    await userEvent.keyboard('{Escape}');
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders nothing when closed', () => {
    render(<Dialog open={false} title="x" onClose={() => {}} />);
    expect(screen.queryByRole('dialog')).toBeNull();
  });

  it('close button calls onClose', async () => {
    const onClose = vi.fn();
    render(<Dialog open title="x" onClose={onClose} />);
    await userEvent.click(screen.getByRole('button', { name: 'Cerrar' }));
    expect(onClose).toHaveBeenCalledOnce();
  });
});

describe('ActionMenu', () => {
  it('opens and runs the selected action', async () => {
    const onSelect = vi.fn();
    render(<ActionMenu actions={[{ icon: 'pause', label: 'Pausar', onSelect }]} />);
    await userEvent.click(screen.getByRole('button', { name: 'Más acciones' }));
    await userEvent.click(await screen.findByRole('menuitem', { name: 'Pausar' }));
    expect(onSelect).toHaveBeenCalledOnce();
  });

  it('DownloadItem collapses its actions into the menu on narrow screens', () => {
    vi.stubGlobal('matchMedia', () => ({ matches: true, addEventListener: () => {}, removeEventListener: () => {} }));
    render(<DownloadItem name="f" status="downloading" />);
    expect(screen.getAllByRole('button')).toHaveLength(1);
    expect(screen.getByRole('button', { name: 'Más acciones' })).toBeInTheDocument();
    vi.unstubAllGlobals();
  });
});

describe('Toasts', () => {
  function Trigger({ tone }: { tone: 'info' | 'error' }) {
    const toast = useToast();
    return <button onClick={() => toast({ tone, message: 'Hecho', actionLabel: 'Copiar ruta' })}>go</button>;
  }

  beforeEach(() => vi.useFakeTimers({ shouldAdvanceTime: true }));
  afterEach(() => vi.useRealTimers());

  it('info toasts close themselves after the timeout', async () => {
    render(<ToastProvider><Trigger tone="info" /></ToastProvider>);
    await userEvent.click(screen.getByText('go'));
    expect(screen.getByRole('status')).toHaveTextContent('Hecho');
    act(() => { vi.advanceTimersByTime(TOAST_TIMEOUT_MS + 1); });
    expect(screen.queryByRole('status')).toBeNull();
  });

  it('error toasts use role=alert and stay until dismissed', async () => {
    render(<ToastProvider><Trigger tone="error" /></ToastProvider>);
    await userEvent.click(screen.getByText('go'));
    act(() => { vi.advanceTimersByTime(TOAST_TIMEOUT_MS * 3); });
    expect(screen.getByRole('alert')).toBeInTheDocument();
    await userEvent.click(screen.getByRole('button', { name: 'Descartar' }));
    expect(screen.queryByRole('alert')).toBeNull();
  });
});
