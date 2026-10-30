import { useId } from 'react';
import { cx } from './cx';

export interface SegmentedProps<T extends string> {
  label: string;
  value: T;
  onChange: (value: T) => void;
  options: { value: T; label: string }[];
}

export function Segmented<T extends string>({ label, value, onChange, options }: SegmentedProps<T>) {
  const name = useId();
  return (
    <fieldset className="m-0 p-0 border-0 min-w-0">
      <legend className="mb-1.5 p-0 text-label text-ink">{label}</legend>
      <div className="inline-flex p-0.5 gap-0.5 border border-line-strong rounded-sm bg-surface-200">
        {options.map((o) => (
          <label
            key={o.value}
            className={cx(
              'px-3 h-8 inline-flex items-center rounded-sm text-body-sm cursor-pointer has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-focus',
              value === o.value ? 'bg-slate-600 text-on-slate' : 'text-ink hover:bg-surface-100',
            )}
          >
            <input
              type="radio"
              name={name}
              value={o.value}
              checked={value === o.value}
              onChange={() => onChange(o.value)}
              className="sr-only"
            />
            {o.label}
          </label>
        ))}
      </div>
    </fieldset>
  );
}
