import { cx } from './cx';
import { strings } from '../../strings';

export type DownloadStatus = 'downloading' | 'queued' | 'paused' | 'completed' | 'failed';

const badge: Record<DownloadStatus, string> = {
  downloading: 'bg-slate-100 text-slate-700',
  queued: 'bg-surface-200 text-ink-muted',
  paused: 'bg-rufous-100 text-rufous-700',
  completed: 'bg-surface-200 text-moss',
  failed: 'bg-surface-200 text-danger',
};

const fill: Record<DownloadStatus, string> = {
  downloading: 'bg-slate-600',
  queued: 'bg-line-strong',
  paused: 'bg-rufous-500',
  completed: 'bg-moss',
  failed: 'bg-danger',
};

export function StatusBadge({ status, children }: { status: DownloadStatus; children?: React.ReactNode }) {
  return (
    <span
      className={cx(
        'inline-flex items-center gap-1.5 h-5.5 px-2.5 rounded-full text-caption tracking-wide whitespace-nowrap',
        'before:content-[""] before:size-1.5 before:rounded-full before:bg-current',
        badge[status],
      )}
    >
      {children ?? strings.status[status]}
    </span>
  );
}

/** `value` is 0–100; omit it for an indeterminate bar. */
export function ProgressBar({ value, status = 'downloading', label, className }: {
  value?: number;
  status?: DownloadStatus;
  label: string;
  className?: string;
}) {
  const indeterminate = value == null;
  const pct = indeterminate ? 0 : Math.max(0, Math.min(100, value));
  return (
    <div
      role="progressbar"
      aria-label={label}
      aria-valuemin={0}
      aria-valuemax={100}
      aria-valuenow={indeterminate ? undefined : Math.round(pct)}
      className={cx('relative h-1.5 rounded-full bg-surface-200 overflow-hidden', className)}
    >
      <div
        className={cx(
          'h-full rounded-full transition-[width] duration-300',
          fill[status],
          indeterminate && 'w-[30%] motion-safe:animate-slide motion-reduce:w-full motion-reduce:opacity-50',
        )}
        style={indeterminate ? undefined : { width: `${pct}%` }}
      />
    </div>
  );
}
