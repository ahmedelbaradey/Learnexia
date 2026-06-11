/**
 * Light + dark themes.
 *
 * Dark is the DEFAULT game-world palette (all 8 in-scope components render on
 * dark). Light is defined for future parent-dashboard/print contexts only.
 * Slot → token mapping follows Design Spec §2c.
 */
import { colors } from '../tokens';

/** Dark theme — default game-world palette. */
export const darkTheme = {
  background: colors.bg,
  backgroundStrong: colors.bgElevated,
  backgroundHover: colors.card,
  backgroundPress: colors.cardSoft,
  backgroundFocus: colors.card,
  backgroundTransparent: 'rgba(15, 23, 42, 0)',

  color: colors.fg1,
  colorHover: colors.fg2,
  colorPress: colors.fg3,
  colorFocus: colors.fg1,
  colorMuted: colors.fg3,

  borderColor: colors.border,
  borderColorHover: colors.borderStrong,
  borderColorFocus: colors.borderFocus,
  borderColorPress: colors.borderStrong,

  shadowColor: colors.overlay,
  shadowColorHover: colors.overlay,

  // brand accents exposed as theme values so components can use `$primary` etc.
  primary: colors.primary,
  primaryHover: colors.primaryHover,
  primaryPress: colors.primaryPress,
  secondary: colors.secondary,
  accent: colors.accent,
  danger: colors.danger,
  purple: colors.purple,
} as const;

/**
 * Light theme — parent/print surface (secondary palette).
 *
 * NOTE: SKILL.md specifies "dark by default" — this is a derived, contrast-safe
 * palette for the parent dashboard and system light-mode preference. There is no
 * canonical light-theme reference in the design kit; values below are derived by
 * inverting the dark-theme surface role relationships onto a light canvas
 * (#F8FAFC) while keeping all 5 primaries and 3 gradient accent colours
 * identical (they read on both canvases). All brand/gamification accent tokens
 * (primary, secondary, accent, danger, purple, xp, streak, heart) are the SAME
 * concrete values as dark so interactive chrome (buttons, badges, chips) stays
 * visually consistent across themes.
 *
 * Surface inversion rule (vs dark theme):
 *   Dark:  bg(#0F172A) → card(#1E293B) → cardSoft(#334155) — step darker
 *   Light: bg(#F8FAFC) → card(#FFFFFF) → cardSoft(#F1F5F9) — step lighter/elevated
 *   Borders use dark-alpha (dark overlaid on light) rather than white-alpha.
 *   Text uses near-black (#0F172A primary) with slate mid-tones for body/muted.
 */
export const lightTheme = {
  // ---- Surfaces ----
  background: colors.bgLight,          // #F8FAFC — light canvas
  backgroundStrong: '#FFFFFF',          // elevated card surface (white)
  backgroundHover: '#F1F5F9',          // slate-100 hover state
  backgroundPress: '#E2E8F0',          // slate-200 press state
  backgroundFocus: '#FFFFFF',          // focused input / card bg
  backgroundTransparent: 'rgba(248, 250, 252, 0)',

  // ---- Text ----
  color: colors.fgInverse,             // #0F172A — primary text (near-black)
  colorHover: '#1E293B',               // slate-800
  colorPress: '#334155',               // slate-700
  colorFocus: colors.fgInverse,
  colorMuted: '#64748B',               // slate-500 — muted / placeholders

  // ---- Borders (dark-alpha on light canvas) ----
  borderColor: 'rgba(15, 23, 42, 0.08)',
  borderColorHover: 'rgba(15, 23, 42, 0.16)',
  borderColorFocus: colors.borderFocus,  // $primary — same on both themes
  borderColorPress: 'rgba(15, 23, 42, 0.16)',

  // ---- Elevation shadows ----
  shadowColor: 'rgba(15, 23, 42, 0.10)',
  shadowColorHover: 'rgba(15, 23, 42, 0.18)',

  // ---- Brand / gamification accents — identical on both themes ----
  primary: colors.primary,
  primaryHover: colors.primaryHover,
  primaryPress: colors.primaryPress,
  secondary: colors.secondary,
  accent: colors.accent,
  danger: colors.danger,
  purple: colors.purple,
} as const;

export const themes = {
  dark: darkTheme,
  light: lightTheme,
} as const;

/** Tamagui defaults to this theme. */
export const DEFAULT_THEME = 'dark' as const;

export type Themes = typeof themes;
export type ThemeName = keyof Themes;
