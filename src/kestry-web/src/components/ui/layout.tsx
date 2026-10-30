import type { ButtonHTMLAttributes, ReactNode } from 'react';
import { cx } from './cx';
import { Icon, type IconName } from './Icon';

export function NavItem({ label, icon, count, active, className, ...rest }: ButtonHTMLAttributes<HTMLButtonElement> & {
  label: string;
  icon?: IconName;
  count?: number;
  active?: boolean;
}) {
  return (
    <button
      type="button"
      {...rest}
      aria-current={active ? 'page' : undefined}
      className={cx(
        'group flex items-center gap-3 w-full h-10 px-3 border-0 rounded-md bg-transparent text-left text-body-sm font-semibold cursor-pointer',
        'text-ink-muted hover:bg-surface-200 hover:text-ink aria-[current=page]:bg-slate-100 aria-[current=page]:text-ink',
        className,
      )}
    >
      {icon && <Icon name={icon} className="group-aria-[current=page]:text-slate-600" />}
      <span className="flex-1">{label}</span>
      {count != null && <span className="text-caption tracking-normal tabular-nums text-ink-muted">{count}</span>}
    </button>
  );
}

export function Toolbar({ label, children, className }: { label: string; children: ReactNode; className?: string }) {
  return (
    <div
      role="toolbar"
      aria-label={label}
      className={cx('flex items-center gap-2 px-4 py-3 bg-surface-000 border-b border-line', className)}
    >
      {children}
    </div>
  );
}
Toolbar.Spacer = function Spacer() {
  return <span className="flex-1" />;
};

export function SpeedStat({ label, value, unit, icon, className }: {
  label: string;
  value: ReactNode;
  unit?: string;
  icon?: IconName;
  className?: string;
}) {
  return (
    <div className={cx('flex flex-col gap-0.5 p-4 bg-surface-100 border border-line rounded-md min-w-35', className)}>
      <span className="flex items-center gap-1.5 text-caption text-ink-muted">
        {icon && <Icon name={icon} size={16} />}
        {label}
      </span>
      <span className="text-heading-2 tabular-nums text-ink">
        {value}
        {unit && <span className="ml-1 text-body-sm font-medium text-ink-muted">{unit}</span>}
      </span>
    </div>
  );
}

export function EmptyState({ title, description, icon = 'download', action, className }: {
  title: string;
  description?: string;
  icon?: IconName;
  action?: ReactNode;
  className?: string;
}) {
  return (
    <div className={cx('flex flex-col items-center gap-2 px-6 py-12 text-center', className)}>
      <div className="grid place-items-center size-16 mb-2 rounded-full bg-rufous-100 text-rufous-700">
        <Icon name={icon} size={28} />
      </div>
      <h3 className="m-0 font-display text-heading-2 font-medium text-ink">{title}</h3>
      {description && <p className="m-0 mb-2 max-w-90 text-body-sm text-ink-muted">{description}</p>}
      {action}
    </div>
  );
}

/** `action` banners use rufous-100 (needs the user's attention); `info` banners use slate-100. */
export function Banner({ tone = 'info', children, className }: { tone?: 'info' | 'action'; children: ReactNode; className?: string }) {
  return (
    <div
      role={tone === 'action' ? 'alert' : 'status'}
      className={cx(
        'flex items-center gap-3 px-4 py-3 rounded-md text-body-sm text-ink',
        tone === 'action' ? 'bg-rufous-100' : 'bg-slate-100',
        className,
      )}
    >
      {children}
    </div>
  );
}
