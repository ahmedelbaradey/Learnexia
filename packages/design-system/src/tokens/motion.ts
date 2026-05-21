/**
 * Motion tokens — durations + easings.
 *
 * Source: `--lx-ease-*` and `--lx-dur-*` in colors_and_type.css.
 * CSS cubic-bezier strings are used on web; RN Reanimated / Moti equivalents
 * are provided alongside (Reanimated has no cubic-bezier string parser — use
 * the `Easing.bezier(...)` argument tuple, and Moti spring config for the
 * overshoot "spring" easing).
 */

/** Durations in milliseconds (numbers, ready for Reanimated `withTiming`). */
export const durations = {
  fast: 120,
  base: 240,
  slow: 400,
  celebrate: 700,
} as const;

/** CSS easing strings (web). */
export const easings = {
  easeOut: 'cubic-bezier(0.16, 1, 0.3, 1)',
  easeSpring: 'cubic-bezier(0.34, 1.56, 0.64, 1)',
} as const;

/** Reanimated `Easing.bezier(...)` argument tuples. */
export const bezier = {
  easeOut: [0.16, 1, 0.3, 1] as const,
  easeSpring: [0.34, 1.56, 0.64, 1] as const,
} as const;

/**
 * Moti spring presets for overshoot pops (badge pop-in, reward icon, etc.).
 * `easeSpring` cubic-bezier overshoot is best reproduced on native via spring.
 */
export const springs = {
  /** Strong overshoot pop (Badge / RewardPopup icon). */
  pop: { damping: 10, stiffness: 200 },
  /** Softer settle (Hearts loss recovery). */
  settle: { damping: 14, stiffness: 150 },
  /** Popup entrance. */
  popup: { damping: 12, stiffness: 180 },
} as const;

export const motion = { durations, easings, bezier, springs } as const;

export type Durations = typeof durations;
export type Easings = typeof easings;
