/**
 * `LearnexiaProvider` — the app-level wrapper around `TamaguiProvider`.
 *
 * Responsibilities:
 *   - Provide the Tamagui config + default theme (dark).
 *   - Delegate direction detection to `@learnexia/shared/i18n` (NEVER
 *     re-implemented here).
 *   - On WEB: call `applyWebDirection(locale)` so `document.dir`/`lang` flip
 *     instantly on locale change.
 *   - On NATIVE: re-export `applyNativeRtl` (from shared) for the app to call
 *     with RN's `I18nManager` + `react-native-restart`. This provider does NOT
 *     import any RN-native module and does NOT auto-restart.
 *   - Expose the active font families per locale via context (`useLocaleFonts`).
 *
 * NATIVE RELOAD CAVEAT:
 *   `I18nManager.forceRTL` only takes full effect after an app RELOAD. When
 *   switching to Arabic on native, the app must call `applyNativeRtl` (see
 *   below) and trigger a restart (e.g. `react-native-restart`). The
 *   `LearnexiaProvider` cannot do this itself — the trigger is the app's
 *   language-switch action.
 */
import { type Locale } from '@learnexia/shared/constants';
import {
  applyWebDirection,
  directionForLocale,
  type Direction,
} from '@learnexia/shared/i18n';
import { TamaguiProvider, type TamaguiProviderProps } from '@tamagui/core';
import React, { createContext, useContext, useEffect, useMemo } from 'react';

import { fontFamilyForLocale } from '../fonts';
import { tamaguiConfig } from '../tamagui.config';
import type { ThemeName } from '../themes';

export interface LocaleContextValue {
  locale: string;
  direction: Direction;
  /** Active font families for the current locale. */
  fonts: { display: string; body: string };
}

const LocaleContext = createContext<LocaleContextValue>({
  locale: 'ar',
  direction: 'rtl',
  fonts: { display: 'Cairo', body: 'Tajawal' },
});

export interface LearnexiaProviderProps
  extends Omit<TamaguiProviderProps, 'config' | 'defaultTheme'> {
  /** Active locale (e.g. 'ar' | 'en'). Drives direction + font selection. */
  locale: string;
  /** Theme name. Defaults to dark (the game-world default). */
  theme?: ThemeName;
  children: React.ReactNode;
}

export function LearnexiaProvider({
  locale,
  theme = 'dark',
  children,
  ...rest
}: LearnexiaProviderProps) {
  const direction = directionForLocale(locale);

  // Web only: flip document dir/lang instantly. No-op when there is no DOM.
  useEffect(() => {
    applyWebDirection(locale as Locale);
  }, [locale]);

  const value = useMemo<LocaleContextValue>(
    () => ({ locale, direction, fonts: fontFamilyForLocale(locale) }),
    [locale, direction],
  );

  return (
    <LocaleContext.Provider value={value}>
      <TamaguiProvider config={tamaguiConfig} defaultTheme={theme} {...rest}>
        {children}
      </TamaguiProvider>
    </LocaleContext.Provider>
  );
}

/** Read the current locale, direction, and active fonts inside the tree. */
export function useLocaleContext(): LocaleContextValue {
  return useContext(LocaleContext);
}

/** Convenience: active font families for the current locale. */
export function useLocaleFonts(): { display: string; body: string } {
  return useContext(LocaleContext).fonts;
}

/**
 * Re-export of `applyNativeRtl` from `@learnexia/shared/i18n`.
 *
 * Call this from the APP (not this package) on a native locale switch, passing
 * RN's `I18nManager` and optionally `react-native-restart`. Returns `true` when
 * a reload is required (direction changed). See the native-reload caveat above.
 */
export { applyNativeRtl } from '@learnexia/shared/i18n';

export { useDirection } from './useDirection';
export type { Direction };
