import { applyTheme, getThemePreference } from './theme';

const mockSystemDark = (dark: boolean) =>
  vi.stubGlobal('matchMedia', () => ({ matches: dark, addEventListener: () => {}, removeEventListener: () => {} }));

describe('theme', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.classList.remove('dark');
  });
  afterEach(() => vi.unstubAllGlobals());

  it('defaults to system', () => expect(getThemePreference()).toBe('system'));

  it('dark adds the class and persists', () => {
    applyTheme('dark');
    expect(document.documentElement).toHaveClass('dark');
    expect(getThemePreference()).toBe('dark');
  });

  it('light removes the class', () => {
    document.documentElement.classList.add('dark');
    applyTheme('light');
    expect(document.documentElement).not.toHaveClass('dark');
  });

  it.each([[true], [false]])('system follows prefers-color-scheme (dark=%s)', (dark) => {
    mockSystemDark(dark);
    applyTheme('system');
    expect(document.documentElement.classList.contains('dark')).toBe(dark);
  });
});
