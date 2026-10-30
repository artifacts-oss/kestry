import { useEffect, useRef, useState } from 'react';
import { api } from '../api/client';
import type { LinksResponse } from '../api/types';
import { Button } from './ui/Button';
import { Checkbox, TextArea, TextField } from './ui/fields';
import { FileDrop } from './ui/FileDrop';
import { Dialog } from './ui/overlays';
import { StatusBadge } from './ui/status';
import { useToast } from './ui/Toaster';
import { strings } from '../strings';

export const PREVIEW_DELAY_MS = 250;

export function AddDownloadsModal({ open, initialText, onClose }: { open: boolean; initialText: string; onClose: () => void }) {
  const toast = useToast();
  const [text, setText] = useState('');
  const [files, setFiles] = useState<File[]>([]);
  const [subfolder, setSubfolder] = useState('');
  const [paused, setPaused] = useState(false);
  const [preview, setPreview] = useState<LinksResponse | null>(null);
  const [busy, setBusy] = useState(false);
  const request = useRef(0);

  useEffect(() => {
    if (open) setText(initialText);
  }, [open, initialText]);

  // Live summary: the server is the only parser, so ask it (debounced) instead of duplicating its rules here.
  useEffect(() => {
    const id = ++request.current;
    if (!text.trim()) {
      setPreview(null);
      return;
    }
    const timer = setTimeout(() => {
      api.previewLinks(text).then((r) => id === request.current && setPreview(r)).catch(() => id === request.current && setPreview(null));
    }, PREVIEW_DELAY_MS);
    return () => clearTimeout(timer);
  }, [text]);

  const reset = () => {
    setText('');
    setFiles([]);
    setSubfolder('');
    setPaused(false);
    setPreview(null);
  };
  const close = () => {
    reset();
    onClose();
  };

  const canSubmit = !busy && ((preview?.valid ?? 0) > 0 || files.length > 0);

  const submit = async () => {
    setBusy(true);
    try {
      let added = 0;
      const options = { subfolder: subfolder.trim() || undefined, paused: paused || undefined };
      if ((preview?.valid ?? 0) > 0) added += (await api.addLinks({ text, ...options })).valid;
      if (files.length > 0) added += (await api.addTorrents(files, options.subfolder, options.paused)).filter((r) => r.status === 'added').length;
      toast({ tone: 'info', message: strings.toasts_added(added) });
      close();
    } catch (e) {
      toast({ tone: 'error', message: (e as Error).message || strings.toasts.requestFailed });
    } finally {
      setBusy(false);
    }
  };

  const invalid = preview?.links.filter((l) => l.state === 'invalid') ?? [];
  const a = strings.add;

  return (
    <Dialog
      open={open}
      title={a.title}
      onClose={close}
      footer={
        <>
          <Button variant="quiet" onClick={close}>{a.cancel}</Button>
          <Button variant="primary" onClick={submit} disabled={!canSubmit}>{a.submit}</Button>
        </>
      }
    >
      <TextArea
        label={a.linksLabel}
        hint={a.linksHint}
        mono
        rows={8}
        placeholder={a.linksPlaceholder}
        value={text}
        onChange={(e) => setText(e.target.value)}
        spellCheck={false}
      />
      {preview && (
        <div className="flex flex-col gap-2" aria-live="polite">
          <div className="flex flex-wrap gap-2">
            <StatusBadge status="completed">{a.valid(preview.valid)}</StatusBadge>
            {preview.duplicates > 0 && <StatusBadge status="paused">{a.duplicates(preview.duplicates)}</StatusBadge>}
            {preview.invalid > 0 && <StatusBadge status="failed">{a.invalid(preview.invalid)}</StatusBadge>}
          </div>
          {invalid.length > 0 && (
            <div className="text-caption tracking-normal text-danger">
              {a.invalidList}
              <ul className="m-0 mt-1 pl-4 font-mono text-mono">
                {invalid.map((l, i) => <li key={i} className="break-all">{l.text}</li>)}
              </ul>
            </div>
          )}
        </div>
      )}
      <FileDrop accept=".torrent" onFiles={setFiles} />
      <details className="text-body-sm text-ink">
        <summary className="cursor-pointer text-label">{a.advanced}</summary>
        <div className="flex flex-col gap-4 pt-4">
          <TextField mono label={a.subfolder} hint={a.subfolderHint} value={subfolder} onChange={(e) => setSubfolder(e.target.value)} />
          <Checkbox label={a.paused} checked={paused} onChange={(e) => setPaused(e.target.checked)} />
        </div>
      </details>
    </Dialog>
  );
}
