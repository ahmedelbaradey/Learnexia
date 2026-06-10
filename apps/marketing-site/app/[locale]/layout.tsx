import type { Metadata } from 'next';
import type { ReactNode } from 'react';

import type { Locale } from '../../lib/copy';
import { COPY } from '../../lib/copy';

interface LocaleLayoutProps {
  children: ReactNode;
  params: Promise<{ locale: string }>;
}

export async function generateMetadata({
  params,
}: {
  params: Promise<{ locale: string }>;
}): Promise<Metadata> {
  const { locale } = await params;
  const safeLocale: Locale = locale === 'ar' ? 'ar' : 'en';
  const C = COPY[safeLocale];

  return {
    title: `${C.brand} — ${C.hero.headlineAccent}`,
    description:
      safeLocale === 'ar'
        ? 'Learnexia تجمع بين معلم ذكاء اصطناعي شخصي وقلوب وسلاسل ونقاط خبرة وشارات. يتعلّم الأطفال الرياضيات والعلوم والإنجليزية والعربية من خلال اللعب.'
        : 'Learnexia mixes a personal AI tutor with hearts, streaks, XP and badges. Kids learn Math, Science, English and Arabic by playing.',
  };
}

/**
 * Per-locale layout segment.
 * <html lang dir> is set in the root app/layout.tsx via the x-locale header
 * from middleware.ts — this layout is a provider-only passthrough.
 */
export default async function LocaleLayout({ children }: LocaleLayoutProps) {
  return <>{children}</>;
}
