import { useId, useState } from 'react';
import { cx } from './cx';
import { Icon } from './Icon';
import { useTranslations } from '../../i18n';

export function FileDrop({ accept, onFiles }: { accept: string; onFiles: (files: File[]) => void }) {
  const t = useTranslations('FileDrop');
  const id = useId();
  const [over, setOver] = useState(false);
  const [count, setCount] = useState(0);
  const exts = accept.split(',').map((e) => e.trim().toLowerCase());
  const take = (list: FileList | null) => {
    const files = Array.from(list ?? []).filter((f) => exts.some((e) => f.name.toLowerCase().endsWith(e)));
    setCount(files.length);
    onFiles(files);
  };
  return (
    <label
      htmlFor={id}
      onDragOver={(e) => { e.preventDefault(); setOver(true); }}
      onDragLeave={() => setOver(false)}
      onDrop={(e) => { e.preventDefault(); setOver(false); take(e.dataTransfer.files); }}
      className={cx(
        'flex flex-col items-center gap-2 p-4 border border-dashed border-line-strong rounded-md text-body-sm text-ink-muted text-center cursor-pointer',
        'has-focus-visible:outline-2 has-focus-visible:outline-offset-2 has-focus-visible:outline-focus',
        over ? 'bg-slate-100' : 'bg-surface-100',
      )}
    >
      <Icon name="upload" size={24} />
      <span>{count ? t('selected', { count }) : t('prompt')}</span>
      <input id={id} type="file" multiple accept={accept} className="sr-only" onChange={(e) => take(e.target.files)} />
    </label>
  );
}
