/**
 * themeStore — the active UI theme (client/UI state).
 *
 * Drives `LearnexiaProvider`'s `theme` prop. Initialized to the product default
 * (dark — the game-world default). Flips instantly on both web + native (no
 * reload required, unlike the RTL direction flip). Mirrors `localeStore`'s shape
 * (Zustand, client/UI only — no server data).
 */
import { DEFAULT_THEME, type ThemeName } from '@learnexia/design-system';
import { create } from 'zustand';

export interface ThemeState {
  theme: ThemeName;
  setTheme: (theme: ThemeName) => void;
  toggleTheme: () => void;
}

export const useThemeStore = create<ThemeState>((set) => ({
  theme: DEFAULT_THEME,
  setTheme: (theme) => set({ theme }),
  toggleTheme: () =>
    set((s) => ({ theme: s.theme === 'dark' ? 'light' : 'dark' })),
}));
