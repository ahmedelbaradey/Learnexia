/**
 * localeStore — the active UI locale (client/UI state).
 *
 * Drives `LearnexiaProvider`'s `locale` prop + i18n language. Initialized to the
 * product default (Arabic-first) and updated after a child logs in (to the
 * child's `preferredLanguage`). On web the direction flips instantly; on native
 * a direction change is gated behind the restart prompt (see RestartPrompt).
 *
 * WEB PERSISTENCE (D-5 fix): on web the chosen locale is written to
 * `localStorage` so it survives a hard page reload and cross-route navigation.
 * On native there is no persistence here — locale is set from the server-side
 * `preferredLanguage` on each app start, and a direction change requires the
 * native restart prompt (RTL flip needs a JS reload). The `localStorage` read
 * happens synchronously at module eval so the initial locale is correct before
 * the first render.
 */
import { DEFAULT_LOCALE, LOCALES, type Locale } from '@learnexia/shared';
import { Platform } from 'react-native';
import { create } from 'zustand';

/** localStorage key for the persisted locale (non-user-facing technical identifier). */
const LOCALE_STORAGE_KEY = 'lx_locale';

/**
 * Read the persisted locale from localStorage (web only).
 * Returns null when not on web, when localStorage is unavailable (SSR/private
 * browsing restrictions), or when the stored value is not a known locale.
 */
function readPersistedLocale(): Locale | null {
  if (Platform.OS !== 'web') return null;
  try {
    const raw = globalThis.localStorage?.getItem(LOCALE_STORAGE_KEY);
    if (raw && (LOCALES as readonly string[]).includes(raw)) {
      return raw as Locale;
    }
  } catch {
    // localStorage blocked (e.g. private-browsing restrictions) — degrade silently.
  }
  return null;
}

/**
 * Write the locale to localStorage (web only). Errors are swallowed so a
 * storage restriction never crashes the app.
 */
function persistLocale(locale: Locale): void {
  if (Platform.OS !== 'web') return;
  try {
    globalThis.localStorage?.setItem(LOCALE_STORAGE_KEY, locale);
  } catch {
    // Storage blocked — degrade silently.
  }
}

export interface LocaleState {
  locale: Locale;
  setLocale: (locale: Locale) => void;
}

export const useLocaleStore = create<LocaleState>((set) => ({
  // On web, hydrate from localStorage on first init so a hard reload or cross-
  // route navigation preserves the user's language choice. Fall back to the
  // product default (Arabic-first) when nothing is persisted.
  locale: readPersistedLocale() ?? DEFAULT_LOCALE,
  setLocale: (locale) => {
    persistLocale(locale);
    set({ locale });
  },
}));
