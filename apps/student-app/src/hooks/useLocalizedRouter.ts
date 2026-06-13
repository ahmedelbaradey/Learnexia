/**
 * useLocalizedRouter — a drop-in replacement for expo-router's `useRouter` that
 * prefixes in-app navigations with the active locale segment (path-based i18n).
 *
 * Routes live under `app/[locale]/...`, so a push to `/(parent)/overview` must
 * become `/ar/(parent)/overview`. This wrapper prepends `/${locale}` to absolute
 * in-app paths (keeping the route group so e.g. `/ar/(child)` stays distinct
 * from the locale splash `/ar`); all other router methods (back, setParams, …)
 * delegate unchanged. The group form is intentionally preserved in the href so
 * group indexes resolve unambiguously.
 *
 * `localeHref` is also exported as a pure helper for any non-hook call site.
 */
import { type Href, useRouter } from 'expo-router';
import { useMemo } from 'react';

import { type Locale } from '@learnexia/shared';

import { useLocale } from './useLocale';

const LOCALE_PREFIX = /^\/(ar|en)(\/|$)/;

/** Prefix an absolute in-app path with the locale segment (idempotent). */
export function localeHref(locale: Locale, path: string): Href {
  if (typeof path !== 'string' || !path.startsWith('/')) return path as Href;
  if (LOCALE_PREFIX.test(path)) return path as Href; // already localized
  if (path === '/') return `/${locale}` as Href; // root → locale splash (no trailing slash)
  return `/${locale}${path}` as Href;
}

type HrefLike = string | { pathname: string; params?: Record<string, unknown> };

function localize(locale: Locale, href: HrefLike): Href {
  if (typeof href === 'string') return localeHref(locale, href);
  if (href && typeof href === 'object' && 'pathname' in href) {
    return { ...href, pathname: localeHref(locale, href.pathname) } as unknown as Href;
  }
  return href as Href;
}

export function useLocalizedRouter() {
  const router = useRouter();
  const { locale } = useLocale();
  return useMemo(
    () => ({
      ...router,
      push: (href: HrefLike) => router.push(localize(locale, href)),
      replace: (href: HrefLike) => router.replace(localize(locale, href)),
      navigate: (href: HrefLike) => router.navigate(localize(locale, href)),
    }),
    [router, locale],
  );
}
