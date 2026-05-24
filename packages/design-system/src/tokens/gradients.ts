/**
 * Gradient constants.
 *
 * Tamagui does not model gradients as native tokens, so these are exported as
 * plain strings (CSS gradient syntax for web) plus decomposed color-stop pairs
 * for Skia / expo-linear-gradient on native.
 *
 * Source: `--lx-grad-*` in colors_and_type.css, plus the gold-shimmer XP-full
 * variant derived from `components-xp-bar.html` (Design Gap 3 note).
 */

export const gradients = {
  /** XP bar fill — green → indigo. Authoritative XP gradient (Design Gap 3). */
  gradXp: 'linear-gradient(90deg, #22C55E, #4F46E5)',
  /** Reward sweep — orange → red. */
  gradReward: 'linear-gradient(90deg, #F59E0B, #EF4444)',
  /** Level-up — purple → indigo (135deg). */
  gradLevelup: 'linear-gradient(135deg, #A855F7, #6366F1)',
  /** XP bar at 100% — gold shimmer. */
  gradXpFull: 'linear-gradient(90deg, #FACC15, #FBBF24, #FACC15)',
  /** Auth split-panel brand background — warm purple → deep indigo (align-login GAP-B). */
  gradBrandPanel: 'linear-gradient(165deg, #4F3FB0 0%, #3B2C8F 50%, #1E1B4B 100%)',
  /** Splash progress fill — violet → indigo (align-splash B3). */
  splashProgress: 'linear-gradient(90deg, #C4B5FD, #818CF8)',
} as const;

/** Decomposed color-stop pairs for Skia / expo-linear-gradient on native. */
export const gradientStops = {
  gradXp: { colors: ['#22C55E', '#4F46E5'], angle: 90 },
  gradReward: { colors: ['#F59E0B', '#EF4444'], angle: 90 },
  gradLevelup: { colors: ['#A855F7', '#6366F1'], angle: 135 },
  gradXpFull: { colors: ['#FACC15', '#FBBF24', '#FACC15'], angle: 90 },
  gradBrandPanel: { colors: ['#4F3FB0', '#3B2C8F', '#1E1B4B'], angle: 165 },

  // Radial disc gradients for Badge / RewardPopup discs.
  badgeBronze: { colors: ['#FCD9B5', '#B45309'] },
  badgeSilver: { colors: ['#F1F5F9', '#94A3B8'] },
  badgeGold: { colors: ['#FDE68A', '#F59E0B'] },
  badgeLegendary: { colors: ['#FBCFE8', '#A855F7', '#4F46E5'] },
  tutorAvatar: { colors: ['#A78BFA', '#6366F1'], angle: 135 },

  /**
   * Splash screen full-bleed background — WARM purple glow → deep indigo.
   * Matches the splash capture's radial center colours (align-splash B2).
   * Used as a top→bottom linear approximation of the radial glow on native.
   */
  splashBg: { colors: ['#4F3FB0', '#3B2C8F', '#241B6A'], angle: 160 },
  /** Splash progress fill — violet → indigo (align-splash B3). */
  splashProgress: { colors: ['#C4B5FD', '#818CF8'], angle: 90 },
} as const;

/** CSS radial-gradient strings for web (Badge discs). */
export const radialGradients = {
  badgeBronze: 'radial-gradient(circle at 30% 30%, #FCD9B5, #B45309)',
  badgeSilver: 'radial-gradient(circle at 30% 30%, #F1F5F9, #94A3B8)',
  badgeGold: 'radial-gradient(circle at 30% 30%, #FDE68A, #F59E0B)',
  badgeLegendary: 'radial-gradient(circle at 30% 30%, #FBCFE8, #A855F7 55%, #4F46E5)',
  tutorAvatar: 'linear-gradient(135deg, #A78BFA, #6366F1)',
  /** Splash background — warm purple radial glow at 50% 45% (align-splash B2). */
  splashBg: 'radial-gradient(circle at 50% 45%, #4F3FB0 0%, #3B2C8F 40%, #241B6A 100%)',
} as const;

export type Gradients = typeof gradients;
export type GradientName = keyof Gradients;
