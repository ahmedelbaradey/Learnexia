/**
 * themeStore — the active UI theme (client/UI state).
 *
 * Drives `LearnexiaProvider`'s `theme` prop. Initialized to the product default
 * (dark — the game-world default). Flips instantly on both web + native (no
 * reload required, unlike the RTL direction flip). Mirrors `localeStore`'s shape
 * (Zustand, client/UI only — no server data).
 *
 * WEB PERSISTENCE (parent-dashboard-uiux Q-A2): on web the chosen theme is
 * written to `localStorage` so it survives a hard page reload. On native there
 * is no persistence here — theme is the product default per session.
 */
import { DEFAULT_THEME, type ThemeName } from '@learnexia/design-system';
import { Platform } from 'react-native';
import { create } from 'zustand';

/** localStorage key for the persisted theme (non-user-facing technical identifier). */
const THEME_STORAGE_KEY = 'lx_theme';

/** Known theme names for validation when reading from localStorage. */
const THEME_NAMES: readonly ThemeName[] = ['dark', 'light'];

/**
 * Read the persisted theme from localStorage (web only).
 * Returns null when not on web, when localStorage is unavailable, or when the
 * stored value is not a known theme name.
 */
function readPersistedTheme(): ThemeName | null {
  if (Platform.OS !== 'web') return null;
  try {
    const raw = globalThis.localStorage?.getItem(THEME_STORAGE_KEY);
    if (raw && (THEME_NAMES as readonly string[]).includes(raw)) {
      return raw as ThemeName;
    }
  } catch {
    // localStorage blocked — degrade silently.
  }
  return null;
}

/**
 * Write the theme to localStorage (web only). Errors are swallowed so a
 * storage restriction never crashes the app.
 */
function persistTheme(theme: ThemeName): void {
  if (Platform.OS !== 'web') return;
  try {
    globalThis.localStorage?.setItem(THEME_STORAGE_KEY, theme);
  } catch {
    // Storage blocked — degrade silently.
  }
}

export interface ThemeState {
  theme: ThemeName;
  setTheme: (theme: ThemeName) => void;
  toggleTheme: () => void;
}

export const useThemeStore = create<ThemeState>((set) => ({
  // On web, hydrate from localStorage on first init so a hard reload preserves
  // the user's theme choice. Fall back to the product default (dark-first).
  theme: readPersistedTheme() ?? DEFAULT_THEME,
  setTheme: (theme) => {
    persistTheme(theme);
    set({ theme });
  },
  toggleTheme: () =>
    set((s) => {
      const next = s.theme === 'dark' ? 'light' : ('dark' as ThemeName);
      persistTheme(next);
      return { theme: next };
    }),
}));
