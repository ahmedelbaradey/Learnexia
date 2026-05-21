/**
 * @learnexia/design-system — Tamagui config, tokens, fonts, themes, RTL.
 *
 * Single source of truth for visual tokens consumed by `@learnexia/ui` and the
 * apps. Components must read tokens from here (no raw hex/px).
 */

// Tamagui config (also augments the @tamagui/core module typing).
export { tamaguiConfig, default as config } from './tamagui.config';
export type { AppConfig } from './tamagui.config';

// Tokens + non-token constants (gradients, shadows, motion).
export {
  tokens,
  colors,
  space,
  size,
  radius,
  zIndex,
  fontSize,
  gradients,
  gradientStops,
  radialGradients,
  shadows,
  nativeShadow,
  durations,
  easings,
  bezier,
  springs,
  motion,
} from './tokens';
export type {
  Tokens,
  ColorTokens,
  ColorToken,
  Gradients,
  GradientName,
  Shadows,
  ShadowName,
  Durations,
  Easings,
} from './tokens';

// Fonts.
export {
  fonts,
  poppinsFont,
  cairoFont,
  tajawalFont,
  fontFamilyForLocale,
} from './fonts';
export type { Fonts } from './fonts';

// Themes.
export { themes, darkTheme, lightTheme, DEFAULT_THEME } from './themes';
export type { Themes, ThemeName } from './themes';

// Media queries.
export { media } from './media';
export type { Media } from './media';

// RTL provider + hooks.
export {
  LearnexiaProvider,
  useLocaleContext,
  useLocaleFonts,
  useDirection,
  applyNativeRtl,
} from './rtl';
export type {
  LearnexiaProviderProps,
  LocaleContextValue,
  Direction,
} from './rtl';
