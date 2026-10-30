import type { ReactNode } from 'react';
import { strings } from '../strings';

export function Header({ children }: { children?: ReactNode }) {
  return (
    <header className="flex items-center gap-3 px-4 py-3 bg-surface-000 border-b border-line">
      {/* Reserved slot until the logo exists. */}
      <div aria-hidden className="size-9 rounded-md bg-slate-100" />
      <span className="font-display text-heading-2 font-medium text-ink">{strings.appName}</span>
      <span className="flex-1" />
      {children}
    </header>
  );
}
