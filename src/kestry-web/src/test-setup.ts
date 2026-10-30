import '@testing-library/jest-dom/vitest';
import { setLocale } from './i18n';

// jsdom has no matchMedia; default to a light, wide screen. Tests can override it with vi.stubGlobal.
if (!window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({ matches: false, media: query, addEventListener: () => {}, removeEventListener: () => {} }) as unknown as MediaQueryList;
}

// Most tests assert the Spanish copy; i18n.test.tsx covers the other languages.
beforeEach(() => setLocale('es'));
