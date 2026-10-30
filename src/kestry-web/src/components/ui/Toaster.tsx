import { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import type { ReactNode } from 'react';
import { cx } from './cx';
import { Icon, type IconName } from './Icon';
import { IconButton } from './Button';
import { useTranslations } from '../../i18n';

export type ToastTone = 'info' | 'success' | 'error';

export interface ToastOptions {
  tone?: ToastTone;
  message: string;
  actionLabel?: string;
  onAction?: () => void;
}

const tones: Record<ToastTone, { icon: IconName; color: string }> = {
  info: { icon: 'info', color: 'text-slate-600' },
  success: { icon: 'check', color: 'text-moss' },
  error: { icon: 'alert', color: 'text-danger' },
};

/** info and success close by themselves; errors stay until dismissed. */
export const TOAST_TIMEOUT_MS = 5000;

export function Toast({ tone = 'info', children, actionLabel, onAction, onClose }: {
  tone?: ToastTone;
  children: ReactNode;
  actionLabel?: string;
  onAction?: () => void;
  onClose?: () => void;
}) {
  const t = useTranslations('Actions');
  return (
    <div
      role={tone === 'error' ? 'alert' : 'status'}
      className="flex items-center gap-3 max-w-105 px-4 py-3 bg-surface-100 border border-line rounded-md shadow-soft text-body-sm text-ink"
    >
      <Icon name={tones[tone].icon} className={cx(tones[tone].color)} />
      <span className="flex-1">{children}</span>
      {actionLabel && (
        <button type="button" onClick={onAction} className="p-0 border-0 bg-transparent text-label text-slate-600 cursor-pointer">
          {actionLabel}
        </button>
      )}
      {onClose && <IconButton icon="x" label={t('dismiss')} size="sm" onClick={onClose} />}
    </div>
  );
}

const ToastContext = createContext<(t: ToastOptions) => void>(() => {});
export const useToast = () => useContext(ToastContext);

export function ToastProvider({ children }: { children: ReactNode }) {
  const [items, setItems] = useState<(ToastOptions & { id: number })[]>([]);
  const nextId = useRef(0);

  const dismiss = useCallback((id: number) => setItems((l) => l.filter((t) => t.id !== id)), []);
  const push = useCallback((t: ToastOptions) => {
    const id = nextId.current++;
    setItems((l) => [...l, { ...t, id }]);
    if (t.tone !== 'error') setTimeout(() => dismiss(id), TOAST_TIMEOUT_MS);
  }, [dismiss]);

  const value = useMemo(() => push, [push]);
  return (
    <ToastContext.Provider value={value}>
      {children}
      <div aria-live="polite" className="fixed bottom-4 right-4 z-60 flex flex-col gap-2">
        {items.map((t) => (
          <Toast
            key={t.id}
            tone={t.tone}
            actionLabel={t.actionLabel}
            onAction={() => { t.onAction?.(); dismiss(t.id); }}
            onClose={() => dismiss(t.id)}
          >
            {t.message}
          </Toast>
        ))}
      </div>
    </ToastContext.Provider>
  );
}
