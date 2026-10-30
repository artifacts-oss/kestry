import type { DownloadStatus } from '../components/ui/status';
import type { IconName } from '../components/ui/Icon';

export type Source = 'http' | 'https' | 'ftp' | 'torrent' | 'magnet';

export interface Download {
  gid: string;
  name: string;
  path: string;
  status: DownloadStatus;
  source: Source;
  fileType: Extract<IconName, 'file' | 'video' | 'archive' | 'music' | 'image'>;
  size: number;
  downloaded: number;
  speed: number;
  uploadSpeed: number;
  eta: number | null;
  connections: number;
  seeders: number;
  fetchingMetadata: boolean;
  seeding: boolean;
  error: string | null;
}

export type Aria2State = 'installing' | 'running' | 'restarting' | 'down';

export interface Snapshot {
  downloads: Download[];
  stats: { downloadSpeed: number; uploadSpeed: number; freeDiskBytes: number | null };
  aria2: { state: Aria2State; reasonCode: string | null; reason: string | null };
  shutdown: number | null;
  settingsVersion: number;
}

export interface SystemInfo {
  version: string;
  aria2Version: string | null;
}
