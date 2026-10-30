import { useId } from 'react';
import type { InputHTMLAttributes, ReactNode, SelectHTMLAttributes, TextareaHTMLAttributes } from 'react';
import { cx } from './cx';
import { Icon, type IconName } from './Icon';

const control =
  'w-full box-border border border-line-strong rounded-sm bg-surface-200 text-ink placeholder:text-ink-muted';

function Field({ id, label, hint, error, children, className }: {
  id: string;
  label?: string;
  hint?: string;
  error?: string;
  children: ReactNode;
  className?: string;
}) {
  return (
    <div className={cx('flex flex-col gap-1.5 min-w-0', className)}>
      {label && <label htmlFor={id} className="text-label text-ink">{label}</label>}
      {children}
      {error ? (
        <span id={`${id}-err`} className="text-caption tracking-normal text-danger">{error}</span>
      ) : (
        hint && <span id={`${id}-hint`} className="text-caption tracking-normal text-ink-muted">{hint}</span>
      )}
    </div>
  );
}

const describedBy = (id: string, hint?: string, error?: string) =>
  error ? `${id}-err` : hint ? `${id}-hint` : undefined;

export interface TextFieldProps extends InputHTMLAttributes<HTMLInputElement> {
  label?: string;
  hint?: string;
  error?: string;
  icon?: IconName;
  mono?: boolean;
}

export function TextField({ label, hint, error, icon, mono, className, ...rest }: TextFieldProps) {
  const id = useId();
  return (
    <Field id={id} label={label} hint={hint} error={error} className={className}>
      <div className="relative flex items-center">
        {icon && <Icon name={icon} size={18} className="absolute left-3 text-ink-muted pointer-events-none" />}
        <input
          type="text"
          {...rest}
          id={id}
          aria-invalid={error ? true : undefined}
          aria-describedby={describedBy(id, hint, error)}
          className={cx(
            control,
            'h-10 px-3',
            icon && 'pl-9.5',
            mono ? 'font-mono text-mono' : 'text-body-sm',
            error && 'border-danger',
          )}
        />
      </div>
    </Field>
  );
}

export interface TextAreaProps extends TextareaHTMLAttributes<HTMLTextAreaElement> {
  label?: string;
  hint?: string;
  error?: string;
  mono?: boolean;
}

export function TextArea({ label, hint, error, mono, className, ...rest }: TextAreaProps) {
  const id = useId();
  return (
    <Field id={id} label={label} hint={hint} error={error} className={className}>
      <textarea
        {...rest}
        id={id}
        aria-invalid={error ? true : undefined}
        aria-describedby={describedBy(id, hint, error)}
        className={cx(control, 'p-3 resize-y', mono ? 'font-mono text-mono' : 'text-body-sm', error && 'border-danger')}
      />
    </Field>
  );
}

export interface SelectProps extends SelectHTMLAttributes<HTMLSelectElement> {
  label?: string;
  hint?: string;
  options: (string | { value: string; label: string })[];
}

export function Select({ label, hint, options, className, ...rest }: SelectProps) {
  const id = useId();
  return (
    <Field id={id} label={label} hint={hint} className={className}>
      <div className="relative flex items-center">
        <select
          {...rest}
          id={id}
          aria-describedby={describedBy(id, hint)}
          className={cx(control, 'h-10 pl-3 pr-9 text-body-sm appearance-none cursor-pointer')}
        >
          {options.map((o) => {
            const { value, label: text } = typeof o === 'string' ? { value: o, label: o } : o;
            return <option key={value} value={value}>{text}</option>;
          })}
        </select>
        <Icon name="chevronDown" size={18} className="absolute right-3 text-ink-muted pointer-events-none" />
      </div>
    </Field>
  );
}

export function Checkbox({ label, className, ...rest }: Omit<InputHTMLAttributes<HTMLInputElement>, 'type'> & { label: ReactNode }) {
  return (
    <label className={cx('inline-flex items-center gap-2 text-body-sm text-ink cursor-pointer', className)}>
      <input type="checkbox" {...rest} className="size-4.5 m-0 accent-slate-600 cursor-pointer" />
      {label}
    </label>
  );
}
