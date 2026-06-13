/**
 * useLocale — convenience accessor for the active locale + its direction.
 * Pairs with `react-i18next`'s `useTranslation` for copy.
 *
 * Path-based i18n: on WEB the locale is read from the URL's `[locale]` segment
 * (`/ar/...`, `/en/...`) so it always matches the address bar; it falls back to
 * the locale store when the segment is absent (e.g. the root redirect) or
 * invalid. On NATIVE (no URL bar) it uses the store. The `[locale]` layout keeps
 * the store in sync with the URL, so both agree.
 */
import { directionForLocale, type Direction, type Locale, LOCALES } from '@learnexia/shared';
import { useGlobalSearchParams } from 'expo-router';
import { Platform } from 'react-native';

import { useLocaleStore } from '../providers/localeStore';

export interface LocaleInfo {
  locale: Locale;
  direction: Direction;
  isRtl: boolean;
}

function isLocale(value: unknown): value is Locale {
  return typeof value === 'string' && (LOCALES as readonly string[]).includes(value);
}

export function useLocale(): LocaleInfo {
  const storeLocale = useLocaleStore((s) => s.locale);
  const params = useGlobalSearchParams<{ locale?: string }>();
  const urlLocale = Platform.OS === 'web' && isLocale(params.locale) ? params.locale : null;
  const locale = urlLocale ?? storeLocale;
  const direction = directionForLocale(locale);
  return { locale, direction, isRtl: direction === 'rtl' };
}
