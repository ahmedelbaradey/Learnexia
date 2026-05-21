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

import { fontSize } from '../tokens';

const sizeScale = {
  1: fontSize[1],
  2: fontSize[2],
  3: fontSize[3],
  4: fontSize[4],
  5: fontSize[5],
  6: fontSize[6],
  7: fontSize[7],
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
  7: 40,
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
  8: -0.02 * 48,
  true: 0,
} as const;

const common = {
  size: sizeScale,
  lineHeight: lineHeightScale,
  weight: weightScale,
  letterSpacing: letterSpacingScale,
} as const;

/** Poppins — English display + body. */
export const poppinsFont = createFont({
  family: 'Poppins',
  ...common,
});

/** Cairo — Arabic display (headings). */
export const cairoFont = createFont({
  family: 'Cairo',
  ...common,
});

/** Tajawal — Arabic body. */
export const tajawalFont = createFont({
  family: 'Tajawal',
  ...common,
});

/**
 * Font map registered with Tamagui. `heading`/`body` default to the English
 * (Poppins) family; the `LearnexiaProvider` swaps the active family per locale
 * by overriding the theme font. The Arabic families are registered under named
 * keys so components / the provider can address them.
 */
export const fonts = {
  heading: poppinsFont,
  body: poppinsFont,
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
