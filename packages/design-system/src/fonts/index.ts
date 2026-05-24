/**
 * Tamagui font objects for the three brand families.
 *
 * GAP 1 RESOLUTION (font file path strategy): the required `.ttf` weights are
 * copied into `packages/design-system/assets/fonts/` so this package is
 * self-contained. On NATIVE the faces are loaded via `require()` of those
 * assets (resolved by Metro); see `faces.native.ts`. On WEB the `.ttf` files are
 * loaded by the host via a CSS `@font-face` stylesheet (or `expo-font`), and the
 * Tamagui font only needs the `family` string — so `face` is omitted on web.
 *
 * The Cairo CSS comment ("still from Google Fonts") is STALE — the full Cairo
 * family is present locally and used here. No Google Fonts `@import` is used in
 * the Tamagui config.
 *
 * Type-build note: under plain `tsc` (no Metro) we do NOT statically import the
 * asset requires (a `require('x.ttf')` has no type). Faces are attached at
 * runtime only when a `loadNativeFaces()` resolver is wired by the app shell in
 * P1-09. The web/SSR path needs only the family name, which is always present.
 */
import { createFont } from '@tamagui/core';
import { Platform } from 'react-native';

import { fontSize } from '../tokens';
import { nativeFaceFor } from './faces.native';

const sizeScale = {
  1: fontSize[1],
  2: fontSize[2],
  3: fontSize[3],
  4: fontSize[4],
  5: fontSize[5],
  6: fontSize[6],
  7: fontSize[7],
  // Splash wordmark step (36px) — align-splash M1. Lives between h1 (7) and display (8).
  wordmark: fontSize.wordmark,
  8: fontSize[8],
  true: fontSize[4],
} as const;

const lineHeightScale = {
  1: 16,
  2: 18,
  3: 21,
  4: 24,
  5: 27,
  6: 32,
  // H1 (32px) at --lx-lh-tight 1.15 = 36.8 ≈ 37 (was 40) — align-login GAP-A.
  7: 37,
  // wordmark (36px) at 1.15 ≈ 41.
  wordmark: 41,
  8: 58,
  true: 24,
} as const;

const weightScale = {
  4: '400',
  5: '500',
  6: '600',
  7: '700',
  8: '800',
  9: '900',
  true: '400',
} as const;

const letterSpacingScale = {
  4: 0,
  6: -0.02 * 24,
  7: -0.02 * 32,
  // wordmark (36px) at --lx-tracking-tight -0.02em = -0.72.
  wordmark: -0.02 * 36,
  8: -0.02 * 48,
  true: 0,
} as const;

const common = {
  size: sizeScale,
  lineHeight: lineHeightScale,
  weight: weightScale,
  letterSpacing: letterSpacingScale,
} as const;

/**
 * The `face` map lets Tamagui resolve a numeric weight to the physical font face
 * registered with expo-font on NATIVE (`{ [weight]: { normal: faceKey } }`). On
 * WEB it's inert (weights resolve through the injected `@font-face` rules), so
 * attaching it on both platforms is safe.
 */

/** Poppins — English display + body. */
export const poppinsFont = createFont({
  family: 'Poppins',
  ...common,
  face: nativeFaceFor('Poppins'),
});

/**
 * WEB font-family STACKS for the default heading/body tokens. Poppins has no
 * Arabic glyphs, so on Arabic screens (`dir=rtl`, `lang=ar`) the browser falls
 * through to the next family — Cairo (headings) / Tajawal (body) — for Arabic
 * characters, while Latin (numbers, the brand name, emails) stays Poppins. This
 * is how `$heading`/`$body` render Arabic in the brand typeface without a
 * per-component swap. NATIVE keeps a single family ('Poppins') because RN does
 * not support comma font stacks; native Arabic font selection is a follow-up.
 */
const isWeb = Platform.OS === 'web';
export const headingFont = createFont({
  family: isWeb ? 'Poppins, Cairo' : 'Poppins',
  ...common,
  face: nativeFaceFor('Poppins'),
});
export const bodyFont = createFont({
  family: isWeb ? 'Poppins, Tajawal' : 'Poppins',
  ...common,
  face: nativeFaceFor('Poppins'),
});

/** Cairo — Arabic display (headings). */
export const cairoFont = createFont({
  family: 'Cairo',
  ...common,
  face: nativeFaceFor('Cairo'),
});

/** Tajawal — Arabic body. */
export const tajawalFont = createFont({
  family: 'Tajawal',
  ...common,
  face: nativeFaceFor('Tajawal'),
});

/**
 * Font map registered with Tamagui. `heading`/`body` default to the English
 * (Poppins) family; the `LearnexiaProvider` swaps the active family per locale
 * by overriding the theme font. The Arabic families are registered under named
 * keys so components / the provider can address them.
 */
export const fonts = {
  heading: headingFont,
  body: bodyFont,
  poppins: poppinsFont,
  cairo: cairoFont,
  tajawal: tajawalFont,
} as const;

/** Font-family names by locale + role — used by `LearnexiaProvider`. */
export const fontFamilyForLocale = (locale: string) =>
  locale === 'ar'
    ? { display: 'Cairo', body: 'Tajawal' }
    : { display: 'Poppins', body: 'Poppins' };

export type Fonts = typeof fonts;

// Font asset loaders. WEB injects `@font-face`; NATIVE loads via expo-font.
export { loadWebFonts } from './webFontFace';
export { nativeFontMap, nativeFaceFor } from './faces.native';
export { fontFaces } from './assets';
export type { FontFaceDescriptor } from './assets';
