import type { SystemInfo } from './types';

export class ApiError extends Error {
  constructor(message: string, public status: number) {
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
    throw new ApiError(data?.error ?? res.statusText, res.status);
  }
  return res.status === 204 ? (undefined as T) : res.json();
}

const enc = encodeURIComponent;

export const api = {
  system: () => request<SystemInfo>('GET', '/api/system'),
  pause: (gid: string) => request<void>('POST', `/api/downloads/${enc(gid)}/pause`),
  resume: (gid: string) => request<void>('POST', `/api/downloads/${enc(gid)}/resume`),
  retry: (gid: string) => request<{ gid: string }>('POST', `/api/downloads/${enc(gid)}/retry`),
  remove: (gid: string, deleteFiles: boolean) => request<void>('DELETE', `/api/downloads/${enc(gid)}?deleteFiles=${deleteFiles}`),
  pauseAll: () => request<void>('POST', '/api/downloads/pause-all'),
  resumeAll: () => request<void>('POST', '/api/downloads/resume-all'),
};
