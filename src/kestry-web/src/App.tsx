import { useEffect } from 'react';
import { ToastProvider } from './components/ui/Toaster';
import { applyTheme, getThemePreference } from './theme';
import { strings } from './strings';

export function App() {
  useEffect(() => applyTheme(getThemePreference()), []);
  return (
    <ToastProvider>
      <h1 className="m-4 font-display text-heading-1 text-ink">{strings.appName}</h1>
    </ToastProvider>
  );
}
