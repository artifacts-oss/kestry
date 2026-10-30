import type { ButtonHTMLAttributes } from 'react';
import { cx } from './cx';
import { Icon, type IconName } from './Icon';

const variants = {
  primary: 'bg-slate-600 text-on-slate hover:bg-slate-700',
  accent: 'bg-rufous-700 text-on-rufous',
  secondary: 'bg-surface-100 text-ink border-line-strong hover:bg-surface-200',
  quiet: 'bg-transparent text-ink hover:bg-surface-200',
  danger: 'bg-danger text-on-danger',
} as const;

export interface ButtonProps extends ButtonHTMLAttributes<HTMLButtonElement> {
  variant?: keyof typeof variants;
  size?: 'sm' | 'md';
  icon?: IconName;
}

export function Button({ variant = 'secondary', size = 'md', icon, className, children, ...rest }: ButtonProps) {
  return (
    <button
      type="button"
      {...rest}
      className={cx(
        'inline-flex items-center justify-center gap-2 border border-transparent rounded-md text-label cursor-pointer transition-colors',
        'disabled:opacity-40 disabled:cursor-not-allowed disabled:pointer-events-none',
        size === 'sm' ? 'h-8 px-3' : 'h-10 px-4',
        variants[variant],
        className,
      )}
    >
      {icon && <Icon name={icon} size={size === 'sm' ? 16 : 18} />}
      {children}
    </button>
  );
}

export interface IconButtonProps extends Omit<ButtonHTMLAttributes<HTMLButtonElement>, 'aria-label'> {
  icon: IconName;
  label: string;
  variant?: 'quiet' | 'secondary';
  size?: 'sm' | 'md';
}

export function IconButton({ icon, label, variant = 'quiet', size = 'md', className, ...rest }: IconButtonProps) {
  return (
    <button
      type="button"
      {...rest}
      aria-label={label}
      title={label}
      className={cx(
        'inline-grid place-items-center border border-transparent bg-transparent cursor-pointer hover:bg-surface-200 hover:text-ink',
        'disabled:opacity-40 disabled:cursor-not-allowed disabled:pointer-events-none',
        size === 'sm' ? 'size-7 rounded-sm' : 'size-9 rounded-md',
        variant === 'secondary' ? 'border-line-strong text-ink' : 'text-ink-muted',
        className,
      )}
    >
      <Icon name={icon} size={size === 'sm' ? 16 : 20} />
    </button>
  );
}
