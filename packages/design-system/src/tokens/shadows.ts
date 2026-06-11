/**
 * Shadow / elevation + glow constants.
 *
 * Source: `--lx-shadow-*` and `--lx-focus-ring` in colors_and_type.css.
 * Vars that resolve via `var()` (shadow-card, shadow-card-hover, the glow
 * shadows, focus-ring) are resolved to concrete strings here — Tamagui does not
 * evaluate CSS var references.
 *
 * These are CSS box-shadow strings (web). On native, components translate them
 * into RN `shadowColor/shadowOffset/shadowRadius/elevation` or a Skia blur layer
 * (glow). They are intentionally NOT registered as Tamagui tokens because RN
 * shadow primitives differ from CSS box-shadow; components consume these strings
 * on web and the `nativeShadow` map below on native.
 */

export const shadows = {
  soft: '0 4px 12px rgba(0, 0, 0, 0.15)',
  float: '0 8px 24px rgba(0, 0, 0, 0.25)',
  card: '0 4px 12px rgba(0, 0, 0, 0.15)', // alias of soft (resolved var)
  cardHover: '0 8px 24px rgba(0, 0, 0, 0.25)', // alias of float (resolved var)
  popup: '0 24px 64px rgba(0, 0, 0, 0.55), inset 0 1px 0 rgba(255, 255, 255, 0.12)',
  primaryGlow: '0 8px 24px rgba(99, 102, 241, 0.45)',
  /** Stronger available-node glow (0.6 alpha, 28px spread — SkillTreeNode available state). */
  primaryGlowStrong: '0 0 28px rgba(99, 102, 241, 0.6)',
  successGlow: '0 8px 20px rgba(34, 197, 94, 0.45)',
  /** Boss/danger node glow (0.55 alpha, 28px spread — SkillTreeNode boss state). */
  dangerGlow: '0 0 28px rgba(239, 68, 68, 0.55)',
  xpGlow: '0 0 24px rgba(250, 204, 21, 0.45)',
  streakGlow: '0 0 24px rgba(251, 146, 60, 0.45)',
  heartGlow: '0 0 24px rgba(251, 113, 133, 0.45)',
  focusRing: '0 0 0 2px #4F46E5, 0 0 0 6px rgba(99, 102, 241, 0.45)',
} as const;

/**
 * RN-friendly shadow descriptors for native (Tamagui maps these onto the
 * platform shadow props). `color` is the glow/shadow color; `radius` the blur.
 */
export const nativeShadow = {
  soft: { color: 'rgba(0,0,0,0.15)', offsetY: 4, radius: 12 },
  float: { color: 'rgba(0,0,0,0.25)', offsetY: 8, radius: 24 },
  popup: { color: 'rgba(0,0,0,0.55)', offsetY: 24, radius: 64 },
  primaryGlow: { color: 'rgba(99,102,241,0.45)', offsetY: 8, radius: 24 },
  primaryGlowStrong: { color: 'rgba(99,102,241,0.6)', offsetY: 0, radius: 28 },
  successGlow: { color: 'rgba(34,197,94,0.45)', offsetY: 8, radius: 20 },
  dangerGlow: { color: 'rgba(239,68,68,0.55)', offsetY: 0, radius: 28 },
  xpGlow: { color: 'rgba(250,204,21,0.45)', offsetY: 0, radius: 24 },
  streakGlow: { color: 'rgba(251,146,60,0.45)', offsetY: 0, radius: 24 },
  heartGlow: { color: 'rgba(251,113,133,0.45)', offsetY: 0, radius: 24 },
} as const;

export type Shadows = typeof shadows;
export type ShadowName = keyof Shadows;
