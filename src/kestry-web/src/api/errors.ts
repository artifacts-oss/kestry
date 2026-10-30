import type { Translator } from '../i18n';

/** Anything carrying an API error code: an ApiError, an ErrorBody. */
interface Coded {
  code?: string;
  params?: Record<string, string | number>;
}

/** Translates an API error through the Errors namespace; unknown codes and non-API failures use `fallback`. */
export function errorText(e: unknown, te: Translator, fallback: string): string {
  const { code, params } = (e ?? {}) as Coded;
  return code && te.has(code) ? te(code, params) : fallback;
}
