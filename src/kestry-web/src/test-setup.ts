import '@testing-library/jest-dom/vitest';

// jsdom has no matchMedia; default to a light, wide screen. Tests can override it with vi.stubGlobal.
if (!window.matchMedia) {
  window.matchMedia = (query: string) =>
    ({ matches: false, media: query, addEventListener: () => {}, removeEventListener: () => {} }) as unknown as MediaQueryList;
}
