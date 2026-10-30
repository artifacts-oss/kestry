import type { ReactNode } from 'react';
import { Dialog as RadixDialog } from 'radix-ui';
import { cx } from './cx';
import { IconButton } from './Button';
import { useTranslations } from '../../i18n';

interface OverlayProps {
  open: boolean;
  title: string;
  onClose: () => void;
  footer?: ReactNode;
  children?: ReactNode;
}

function Sheet({ open, title, onClose, footer, children, panel }: OverlayProps & { panel: string }) {
  const t = useTranslations('Actions');
  return (
    <RadixDialog.Root open={open} onOpenChange={(o) => !o && onClose()}>
      <RadixDialog.Portal>
        <RadixDialog.Overlay className="fixed inset-0 bg-ink/40" />
        <RadixDialog.Content
          aria-describedby={undefined}
          className={cx('fixed z-50 flex flex-col bg-surface-000 border border-line shadow-soft', panel)}
        >
          <div className="flex items-center justify-between px-6 py-4 border-b border-line">
            <RadixDialog.Title className="m-0 text-heading-2 text-ink">{title}</RadixDialog.Title>
            <RadixDialog.Close asChild>
              <IconButton icon="x" label={t('close')} />
            </RadixDialog.Close>
          </div>
          <div className="flex flex-col flex-1 gap-4 p-6 overflow-y-auto">{children}</div>
          {footer && <div className="flex justify-end gap-2 px-6 py-4 border-t border-line">{footer}</div>}
        </RadixDialog.Content>
      </RadixDialog.Portal>
    </RadixDialog.Root>
  );
}

export const Dialog = (p: OverlayProps) => (
  <Sheet {...p} panel="top-1/2 left-1/2 -translate-x-1/2 -translate-y-1/2 w-[calc(100%-2rem)] max-w-120 max-h-[90vh] rounded-lg" />
);

/** Side panel anchored to the right; full width below `sm`. */
export const Drawer = (p: OverlayProps) => (
  <Sheet {...p} panel="inset-y-0 right-0 w-full sm:w-120 rounded-none sm:rounded-l-lg" />
);
