import type { Download } from '../api/types';
import { DownloadItem } from './ui/DownloadItem';

export interface DownloadActions {
  onPause: (d: Download) => void;
  onResume: (d: Download) => void;
  onCancel: (d: Download) => void;
  onRetry: (d: Download) => void;
  onRemove: (d: Download) => void;
  onCopyPath: (d: Download) => void;
}

export function DownloadsList({ downloads, actions }: { downloads: Download[]; actions: DownloadActions }) {
  return (
    <ul className="flex flex-col gap-2 m-0 p-0 list-none">
      {downloads.map((d) => (
        <li key={d.gid}>
          <DownloadItem
            name={d.name}
            path={d.path}
            status={d.status}
            fileType={d.fileType}
            source={d.source}
            size={d.size}
            downloaded={d.downloaded}
            speed={d.speed}
            uploadSpeed={d.uploadSpeed}
            eta={d.eta ?? undefined}
            connections={d.connections}
            seeders={d.seeders}
            fetchingMetadata={d.fetchingMetadata}
            error={d.error ?? undefined}
            onPause={() => actions.onPause(d)}
            onResume={() => actions.onResume(d)}
            onCancel={() => actions.onCancel(d)}
            onRetry={() => actions.onRetry(d)}
            onRemove={() => actions.onRemove(d)}
            onCopyPath={() => actions.onCopyPath(d)}
          />
        </li>
      ))}
    </ul>
  );
}
