/**
 * Learnexia color tokens.
 *
 * Source of truth: `design-system/colors_and_type.css` (`--lx-*` custom properties).
 * Tamagui does NOT evaluate CSS `var()` references, so every value that the CSS
 * authors via `var(--other)` is resolved to its concrete hex/rgba value here.
 *
 * Formerly TS-only gap-fills, now promoted into colors_and_type.css too (the
 * pixel-alignment pass requires CSS↔TS parity):
 *   - `fg4` (#64748B) — disabled/locked/inactive muted text
 *   - `primaryLight` (#A5B4FC) — active nav/tab label, eyebrow, grade pills
 *   - `purpleLight` (#E9D5FF) — Legendary badge label
 * Plus the alignment-pass additions: `xpSoft`/`streakSoft` (0.13 KPI tints),
 * `borderInput` (0.10), `borderSubtle` (0.06), `fg2Alpha` (0.70 white).
 */

export const colors = {
  // ---- Primary palette ----
  primary: '#4F46E5',
  primaryHover: '#6366F1',
  primaryPress: '#4338CA',
  primarySoft: 'rgba(79, 70, 229, 0.18)',
  /** Stronger active-pill tint (Tabs/Sidebar active item) — Design Gap GAP-02. */
  primarySoftStrong: 'rgba(79, 70, 229, 0.28)',
  primaryGlow: 'rgba(99, 102, 241, 0.45)',

  // ---- Accent / status ----
  secondary: '#22C55E',
  accent: '#F59E0B',
  danger: '#EF4444',
  purple: '#A855F7',

  // semantic aliases (CSS uses var() — resolved to concrete values)
  success: '#22C55E', // var(--lx-secondary)
  successSoft: 'rgba(34, 197, 94, 0.18)',
  warning: '#F59E0B', // var(--lx-accent)
  warningSoft: 'rgba(245, 158, 11, 0.18)',
  dangerSoft: 'rgba(239, 68, 68, 0.18)',
  purpleSoft: 'rgba(168, 85, 247, 0.18)',

  // ---- Gamification accents ----
  xp: '#FACC15',
  xpGlow: 'rgba(250, 204, 21, 0.45)',
  /** ~0.13 alpha XP tint for KPI icon chips (align-overview GAP-02). */
  xpSoft: 'rgba(250, 204, 21, 0.13)',
  streak: '#FB923C',
  streakGlow: 'rgba(251, 146, 60, 0.45)',
  /** ~0.13 alpha streak tint for KPI icon chips (align-overview GAP-02). */
  streakSoft: 'rgba(251, 146, 60, 0.13)',
  heart: '#FB7185',
  heartGlow: 'rgba(251, 113, 133, 0.45)',
  gold: '#FBBF24',
  gem: '#38BDF8',

  // ---- Surfaces (dark theme) ----
  bg: '#0F172A',
  bgElevated: '#111B33',
  card: '#1E293B',
  cardSoft: '#334155',
  bgLight: '#F8FAFC',
  overlay: 'rgba(15, 23, 42, 0.72)',

  // ---- Text ----
  fg1: '#F8FAFC',
  fg2: '#CBD5E1',
  fg3: '#94A3B8',
  fgInverse: '#0F172A',
  /** Disabled-button + locked-badge + inactive-status muted text. */
  fg4: '#64748B',
  /** 70%-alpha white for splash/overlay text on the purple bg (align-splash M5/M6/M9). */
  fg2Alpha: 'rgba(255, 255, 255, 0.70)',

  // ---- Borders ----
  border: 'rgba(255, 255, 255, 0.08)',
  borderStrong: 'rgba(255, 255, 255, 0.16)',
  borderFocus: '#4F46E5', // var(--lx-primary)
  /** Input resting border at 0.10 alpha (components-input.html — align-login GAP-C). */
  borderInput: 'rgba(255, 255, 255, 0.10)',
  /** Subtle 0.06-alpha hairline (sidebar/header dividers — align-my-children DG-5). */
  borderSubtle: 'rgba(255, 255, 255, 0.06)',

  // ---- Gap-fill inline accent shades (Design Gap 4) ----
  /** GAP-FILL (not in CSS): indigo-300 for AITutorBubble chip text. */
  primaryLight: '#A5B4FC',
  /** GAP-FILL (not in CSS): purple-200 for Legendary badge label. */
  purpleLight: '#E9D5FF',
} as const;

export type ColorTokens = typeof colors;
export type ColorToken = keyof ColorTokens;
