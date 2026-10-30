import { useEffect, useState } from 'react';
import type { Snapshot } from './types';

/** Subscribes to the server's snapshot stream; EventSource reconnects by itself. */
export function useSnapshot(): { snapshot: Snapshot | null; connected: boolean } {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    const source = new EventSource('/api/events');
    source.onopen = () => setConnected(true);
    source.onerror = () => setConnected(false);
    source.addEventListener('snapshot', (e) => {
      setConnected(true);
      setSnapshot(JSON.parse((e as MessageEvent<string>).data));
    });
    return () => source.close();
  }, []);

  return { snapshot, connected };
}
