import { useId } from 'react';
import { Switch as RadixSwitch } from 'radix-ui';
import { cx } from './cx';

export interface SwitchProps {
  checked: boolean;
  onChange?: (checked: boolean) => void;
  label?: string;
  disabled?: boolean;
  className?: string;
}

export function Switch({ checked, onChange, label, disabled, className }: SwitchProps) {
  const id = useId();
  return (
    <span className={cx('inline-flex items-center gap-3 text-body-sm text-ink', className)}>
      <RadixSwitch.Root
        id={id}
        checked={checked}
        onCheckedChange={onChange}
        disabled={disabled}
        className={cx(
          'group relative w-10 h-6 p-0 border border-line-strong rounded-full bg-surface-200 cursor-pointer transition-colors',
          'data-[state=checked]:bg-slate-600 data-[state=checked]:border-slate-600',
          'disabled:opacity-40 disabled:cursor-not-allowed',
        )}
      >
        <RadixSwitch.Thumb className="block size-4 ml-0.75 rounded-full bg-ink-muted transition-transform data-[state=checked]:translate-x-4 data-[state=checked]:bg-on-slate" />
      </RadixSwitch.Root>
      {label && <label htmlFor={id}>{label}</label>}
    </span>
  );
}
