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
}

export default tamaguiConfig;
