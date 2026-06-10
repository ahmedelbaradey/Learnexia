import type { Viewport } from 'next';
import type { ReactNode } from 'react';
import { headers } from 'next/headers';

import './globals.css';

/**
 * Root layout — loads globals.css (fonts + tokens) and sets the correct
 * <html lang dir> for the current locale by reading the x-locale header
 * injected by middleware.ts.
 *
 * Locale routing: middleware.ts redirects / → /en and injects x-locale on
 * every /en or /ar request. /ar → lang="ar" dir="rtl"; default → "en" ltr.
 *
 * The <html> element can only live in the root layout (Next.js constraint),
 * so the per-locale app/[locale]/layout.tsx is a provider-only wrapper —
 * it does NOT render <html>.
 */
export const viewport: Viewport = {
  themeColor: '#0F172A',
  width: 'device-width',
  initialScale: 1,
};

export default async function RootLayout({ children }: { children: ReactNode }) {
  const hdrs = await headers();
  const locale = hdrs.get('x-locale') ?? 'en';
  const lang = locale === 'ar' ? 'ar' : 'en';
  const dir = locale === 'ar' ? 'rtl' : 'ltr';

  return (
    <html lang={lang} dir={dir}>
      <body>{children}</body>
    </html>
  );
}
