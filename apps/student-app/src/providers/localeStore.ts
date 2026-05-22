/**
 * localeStore — the active UI locale (client/UI state).
 *
 * Drives `LearnexiaProvider`'s `locale` prop + i18n language. Initialized to the
 * product default (Arabic-first) and updated after a child logs in (to the
 * child's `preferredLanguage`). On web the direction flips instantly; on native
 * a direction change is gated behind the restart prompt (see RestartPrompt).
 */
import { DEFAULT_LOCALE, type Locale } from '@learnexia/shared';
import { create } from 'zustand';

export interface LocaleState {
  locale: Locale;
  setLocale: (locale: Locale) => void;
}

export const useLocaleStore = create<LocaleState>((set) => ({
  locale: DEFAULT_LOCALE,
  setLocale: (locale) => set({ locale }),
}));
