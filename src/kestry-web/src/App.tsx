import { useEffect, useState } from 'react';
import { api } from './api/client';
import { useSnapshot } from './api/useSnapshot';
import { ToastProvider } from './components/ui/Toaster';
import { DownloadsPage } from './pages/DownloadsPage';
import { applyTheme, getThemePreference } from './theme';

export function App() {
  const { snapshot, connected } = useSnapshot();
  const [aria2Version, setAria2Version] = useState<string | null>(null);
  const state = snapshot?.aria2.state;

  useEffect(() => applyTheme(getThemePreference()), []);
  // The version is only known once aria2 is up, so ask again whenever its state changes.
  useEffect(() => {
    if (state === 'running') api.system().then((s) => setAria2Version(s.aria2Version)).catch(() => {});
  }, [state]);

  return (
    <ToastProvider>
      <DownloadsPage snapshot={snapshot} connected={connected} aria2Version={aria2Version} />
    </ToastProvider>
  );
}
