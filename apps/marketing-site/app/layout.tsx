import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';

import './globals.css';
import { LANDING_COPY } from '../lib/copy';

export const metadata: Metadata = {
  title: `${LANDING_COPY.brand} — ${LANDING_COPY.hero.headlineAccent} that teaches`,
  description:
    'Learnexia mixes a personal AI tutor with hearts, streaks, XP and badges. Kids learn Math, Science, English and Arabic by playing.',
};

export const viewport: Viewport = {
  themeColor: '#0F172A',
  width: 'device-width',
  initialScale: 1,
};

export default function RootLayout({ children }: { children: ReactNode }) {
  // Marketing site is English-first (per task); the design capture is dark.
  return (
    <html lang="en" dir="ltr">
      <body>{children}</body>
    </html>
  );
}
