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

/** Light theme — parent/print surface (secondary palette). */
export const lightTheme = {
  background: colors.bgLight,
  backgroundStrong: '#FFFFFF',
  backgroundHover: '#F1F5F9',
  backgroundPress: '#E2E8F0',
  backgroundFocus: '#FFFFFF',
  backgroundTransparent: 'rgba(248, 250, 252, 0)',

  color: colors.fgInverse,
  colorHover: '#1E293B',
  colorPress: '#334155',
  colorFocus: colors.fgInverse,
  colorMuted: '#64748B',

  borderColor: 'rgba(0, 0, 0, 0.08)',
  borderColorHover: 'rgba(0, 0, 0, 0.16)',
  borderColorFocus: colors.borderFocus,
  borderColorPress: 'rgba(0, 0, 0, 0.16)',

  shadowColor: 'rgba(15, 23, 42, 0.18)',
  shadowColorHover: 'rgba(15, 23, 42, 0.24)',

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
