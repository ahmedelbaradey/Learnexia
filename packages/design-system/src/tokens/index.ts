/**
 * Token barrel + Tamagui `createTokens` input.
 *
 * Numeric scales (space, size, radius, zIndex) are encoded in the shape
 * Tamagui expects (`createTokens`). Colors are spread from `./colors`. Gradient,
 * shadow and motion constants are NOT Tamagui tokens (Tamagui has no native
 * model for them) and are re-exported as plain constants for components.
 *
 * Token key scheme (documented per plan):
 *   - space / size keys mirror the CSS step suffix (`$1`=4px … `$16`=64px)
 *   - radius keys are semantic (`$sm`, `$button`, `$card`, `$modal`, `$pill`)
 */
import { createTokens } from '@tamagui/core';

import { colors } from './colors';

export { colors } from './colors';
export type { ColorTokens, ColorToken } from './colors';
export { gradients, gradientStops, radialGradients } from './gradients';
export type { Gradients, GradientName } from './gradients';
export { shadows, nativeShadow } from './shadows';
export type { Shadows, ShadowName } from './shadows';
export { durations, easings, bezier, springs, motion } from './motion';
export type { Durations, Easings } from './motion';

/** Spacing scale — CSS `--lx-space-*` (px). Keys mirror the step suffix. */
export const space = {
  0: 0,
  1: 4,
  2: 8,
  3: 12,
  4: 16,
  5: 20,
  6: 24,
  8: 32,
  10: 40,
  12: 48,
  16: 64,
  true: 16, // Tamagui requires a `true` default space key
} as const;

/** Size scale — reuse the spacing values plus component-specific sizes. */
export const size = {
  0: 0,
  1: 4,
  2: 8,
  3: 12,
  4: 16,
  5: 20,
  6: 24,
  8: 32,
  10: 40,
  12: 48,
  16: 64,
  // typography scale steps (px) — used as fontSize via font config but handy as raw sizes
  true: 16,
} as const;

/** Radius scale — CSS `--lx-radius-*` (px). Semantic keys. */
export const radius = {
  0: 0,
  sm: 8,
  button: 16,
  card: 20,
  modal: 24,
  pill: 9999,
  true: 16,
} as const;

export const zIndex = {
  0: 0,
  1: 100,
  2: 200,
  3: 300,
  4: 400,
  5: 500,
  true: 0,
} as const;

/**
 * Typography size scale (px) — CSS `--lx-size-*` plus micro/display steps.
 * Mirrors the Design Spec §2b. Re-exported for direct use; the font config
 * (`fonts/index.ts`) uses the same step values.
 */
export const fontSize = {
  1: 10, // micro labels
  2: 12, // captions, chips
  3: 14, // body small
  4: 16, // body
  5: 18, // h3
  6: 24, // h2
  7: 32, // h1
  8: 48, // display
} as const;

/**
 * Tamagui token object. Colors are the resolved palette; numeric scales are the
 * px values above. Gradients/shadows/motion are deliberately excluded (handled
 * as constants).
 */
export const tokens = createTokens({
  color: colors,
  space,
  size,
  radius,
  zIndex,
});

export type Tokens = typeof tokens;
