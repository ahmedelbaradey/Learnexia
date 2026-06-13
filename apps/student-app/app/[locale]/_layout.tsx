/**
 * Locale segment layout — the URL's first segment (`/ar/...`, `/en/...`) is the
 * source of truth for the active locale on web. It validates the `[locale]`
 * param and syncs it into the locale store, which is the app's SINGLE locale
 * source: the root `_layout` derives i18n language, web/native direction, and
 * the Tamagui provider locale from that store. Unknown locales redirect to the
 * default. Native has no URL bar; the root index supplies a default locale.
 */
import { DEFAULT_LOCALE, LOCALES, type Locale } from '@learnexia/shared';
import { Redirect, Slot, useGlobalSearchParams } from 'expo-router';
import { useEffect } from 'react';

import { useLocaleStore } from '@/providers/localeStore';

function isLocale(value: string | undefined): value is Locale {
  return Boolean(value) && (LOCALES as readonly string[]).includes(value as string);
}

export default function LocaleLayout() {
  const { locale } = useGlobalSearchParams<{ locale?: string }>();
  const setLocale = useLocaleStore((s) => s.setLocale);
  const current = useLocaleStore((s) => s.locale);
  const valid = isLocale(locale);

  // Push the URL locale into the store (the app's single locale source). The
  // root _layout reacts to the store for i18n + direction + provider.
  useEffect(() => {
    if (valid && locale !== current) setLocale(locale);
  }, [valid, locale, current, setLocale]);

  if (!valid) {
    return <Redirect href={`/${DEFAULT_LOCALE}`} />;
  }
  return <Slot />;
}
