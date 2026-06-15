/**
 * Tamagui root configuration.
 *
 * Assembles tokens (colors + numeric scales), fonts (Poppins/Cairo/Tajawal),
 * themes (dark default + light), and media queries into a single
 * `createTamagui` config. Gradients / shadows / motion are NOT Tamagui tokens
 * (see their token files) — they are exported as constants and consumed
 * directly by components.
 */
import { createTamagui } from '@tamagui/core';

import { fonts } from './fonts';
import { media } from './media';
import { themes } from './themes';
import { tokens } from './tokens';

export const tamaguiConfig = createTamagui({
  tokens,
  themes,
  fonts,
  media,
  defaultTheme: 'dark',
  shouldAddPrefersColorThemes: false,
  themeClassNameOnRoot: false,
  // Tamagui v1 settings — keep defaults conservative for cross-platform builds.
  settings: {
    allowedStyleValues: 'somewhat-strict',
    fastSchemeChange: true,
  },
});

export type AppConfig = typeof tamaguiConfig;

/**
 * Module augmentation so `styled()` / theme props are fully typed across the
 * monorepo. Consumers get autocompletion for `$primary`, `$card`, `$4`, etc.
 */
declare module '@tamagui/core' {
  // eslint-disable-next-line @typescript-eslint/no-empty-object-type
  interface TamaguiCustomConfig extends AppConfig {}

  /**
   * `dir` is a valid HTML attribute Tamagui forwards to the DOM on web (it's how
   * our RTL-aware components drive layout mirroring — see the dir-based RTL
   * pattern), but it's missing from Tamagui's prop types. Declare it here so any
   * Stack/View can take `dir` without a per-call `@ts-expect-error`.
   */
  interface RNTamaguiViewNonStyleProps {
    dir?: 'ltr' | 'rtl' | 'auto';
  }
}

export default tamaguiConfig;
