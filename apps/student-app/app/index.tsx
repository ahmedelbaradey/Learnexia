/**
 * Root index — redirects `/` to the locale-prefixed app (`/ar` or `/en`).
 *
 * Path-based localization (P-locale): every screen lives under `app/[locale]/`,
 * so the bare root must hand off to a locale segment. The active locale comes
 * from the locale store (hydrated from localStorage on web, `DEFAULT_LOCALE`
 * otherwise), so a returning user keeps their language and native boots into a
 * valid locale segment. The real splash + auth guard live at
 * `app/[locale]/index.tsx`.
 */
import { type Href, Redirect } from 'expo-router';

import { useLocaleStore } from '@/providers/localeStore';

export default function RootIndex() {
  const locale = useLocaleStore((s) => s.locale);
  return <Redirect href={`/${locale}` as Href} />;
}
