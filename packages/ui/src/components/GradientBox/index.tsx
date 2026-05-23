/**
 * GradientBox — public, token-driven linear-gradient surface.
 *
 * Thin re-export of the internal `GradientFill` so apps can render gradient
 * backgrounds (e.g. the parent dashboard family-summary strip) without reaching
 * into `@learnexia/ui` internals. Pass `stops` from the design-system
 * `gradientStops` (no raw hex in app code). Renders via `expo-linear-gradient`
 * when present, else a flat first-stop fallback.
 */
export { GradientFill as GradientBox } from '../../internal/GradientFill';
export type { GradientFillProps as GradientBoxProps } from '../../internal/GradientFill';
