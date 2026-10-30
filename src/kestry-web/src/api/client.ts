import type { AppSettings, ErrorBody, LinksResponse, SystemInfo, TorrentResult } from './types';

export class ApiError extends Error {
  constructor(
    message: string,
    public status: number,
    public code?: string,
    public params?: ErrorBody['params'],
    public fieldErrors: Record<string, ErrorBody> = {},
  ) {
    super(message);
  }
}

async function request<T>(method: string, url: string, body?: BodyInit | object): Promise<T> {
  const isForm = body instanceof FormData;
  const res = await fetch(url, {
    method,
    headers: body && !isForm ? { 'Content-Type': 'application/json' } : undefined,
    body: body && !isForm ? JSON.stringify(body) : (body as FormData | undefined),
  });
  if (!res.ok) {
    const data = await res.json().catch(() => null);
    // Kestrel answers an oversized body with an empty 413, so that one has no code of its own.
    const code = data?.code ?? (res.status === 413 ? 'request.tooLarge' : undefined);
    throw new ApiError(data?.error ?? res.statusText, res.status, code, data?.params, data?.errors);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

const enc = encodeURIComponent;

export const api = {
  system: () => request<SystemInfo>('GET', '/api/system'),
  previewLinks: (text: string) => request<LinksResponse>('POST', '/api/downloads/links/preview', { text }),
  addLinks: (body: { text: string; subfolder?: string; paused?: boolean }) => request<LinksResponse>('POST', '/api/downloads/links', body),
  addTorrents: (files: File[], subfolder?: string, paused?: boolean) => {
    const form = new FormData();
    files.forEach((f) => form.append('files', f));
    const query = new URLSearchParams();
    if (subfolder) query.set('subfolder', subfolder);
    if (paused) query.set('paused', 'true');
    return request<TorrentResult[]>('POST', `/api/downloads/torrents?${query}`, form);
  },
  getSettings: () => request<AppSettings>('GET', '/api/settings'),
  saveSettings: (settings: AppSettings) => request<AppSettings>('PUT', '/api/settings', settings),
  cancelShutdown: () => request<void>('POST', '/api/shutdown/cancel'),
  pause: (gid: string) => request<void>('POST', `/api/downloads/${enc(gid)}/pause`),
  resume: (gid: string) => request<void>('POST', `/api/downloads/${enc(gid)}/resume`),
  retry: (gid: string) => request<{ gid: string }>('POST', `/api/downloads/${enc(gid)}/retry`),
  remove: (gid: string, deleteFiles: boolean) => request<void>('DELETE', `/api/downloads/${enc(gid)}?deleteFiles=${deleteFiles}`),
  pauseAll: () => request<void>('POST', '/api/downloads/pause-all'),
  resumeAll: () => request<void>('POST', '/api/downloads/resume-all'),
};
