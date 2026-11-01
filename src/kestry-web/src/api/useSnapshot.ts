import { useEffect, useState } from 'react';
import type { Snapshot } from './types';

/** The server sends a snapshot every second; this much silence means the stream is dead. */
export const STALE_MS = 5000;

/**
 * Subscribes to the server's snapshot stream. EventSource only retries network errors: it gives up on an
 * HTTP error (e.g. a proxy's 502 while the server restarts) and never notices a half-open connection
 * (sleep, Wi-Fi change, proxy idle timeout), so a watchdog reopens the stream when it goes quiet.
 */
export function useSnapshot(): { snapshot: Snapshot | null; connected: boolean } {
  const [snapshot, setSnapshot] = useState<Snapshot | null>(null);
  const [connected, setConnected] = useState(false);

  useEffect(() => {
    let source: EventSource;
    let lastEvent = Date.now();
    const open = () => {
      source = new EventSource('/api/events');
      source.onerror = () => setConnected(false);
      source.addEventListener('snapshot', (e) => {
        lastEvent = Date.now();
        setConnected(true);
        setSnapshot(JSON.parse((e as MessageEvent<string>).data));
      });
    };
    open();
    const watchdog = setInterval(() => {
      if (Date.now() - lastEvent < STALE_MS) return;
      setConnected(false);
      source.close();
      lastEvent = Date.now();
      open();
    }, 1000);
    return () => {
      clearInterval(watchdog);
      source.close();
    };
  }, []);

  return { snapshot, connected };
}
