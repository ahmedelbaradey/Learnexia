import type { Metadata, Viewport } from 'next';
import type { ReactNode } from 'react';

import './globals.css';
import { TamaguiCssProvider } from './tamagui-css-provider';
import { Providers } from './providers';
import { ADMIN_LOCALE, getStrings } from '../lib/strings';

const strings = getStrings(ADMIN_LOCALE);

export const metadata: Metadata = {
  title: strings.loginPageTitle,
  description: 'Learnexia administration portal.',
  robots: { index: false, follow: false },
};

export const viewport: Viewport = {
  themeColor: '#0F172A',
  width: 'device-width',
  initialScale: 1,
};

export default function RootLayout({ children }: { children: ReactNode }) {
  // English-first; LearnexiaProvider also flips document.dir at runtime, but we
  // set the SSR defaults here so the first paint is correct (no flicker).
  return (
    <html lang={ADMIN_LOCALE} dir="ltr" suppressHydrationWarning>
      <body>
        <TamaguiCssProvider>
          <Providers>{children}</Providers>
        </TamaguiCssProvider>
      </body>
    </html>
  );
}
