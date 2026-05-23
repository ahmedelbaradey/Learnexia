/**
 * Learnexia color tokens.
 *
 * Source of truth: `design-system/colors_and_type.css` (`--lx-*` custom properties).
 * Tamagui does NOT evaluate CSS `var()` references, so every value that the CSS
 * authors via `var(--other)` is resolved to its concrete hex/rgba value here.
 *
 * Gap-fill tokens (NOT present in colors_and_type.css) are flagged inline:
 *   - `fg4` (#64748B) — disabled/locked muted text (Design Gap 2 / 4)
 *   - `primaryLight` (#A5B4FC) — AITutorBubble chip text (Design Gap 4)
 *   - `purpleLight` (#E9D5FF) — Legendary badge label (Design Gap 4)
 * These three are required by the component previews but have no `--lx-*` var.
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
  streak: '#FB923C',
  streakGlow: 'rgba(251, 146, 60, 0.45)',
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
  /** GAP-FILL (not in CSS): disabled-button + locked-badge muted text. Design Gap 2/4. */
  fg4: '#64748B',

  // ---- Borders ----
  border: 'rgba(255, 255, 255, 0.08)',
  borderStrong: 'rgba(255, 255, 255, 0.16)',
  borderFocus: '#4F46E5', // var(--lx-primary)

  // ---- Gap-fill inline accent shades (Design Gap 4) ----
  /** GAP-FILL (not in CSS): indigo-300 for AITutorBubble chip text. */
  primaryLight: '#A5B4FC',
  /** GAP-FILL (not in CSS): purple-200 for Legendary badge label. */
  purpleLight: '#E9D5FF',
} as const;

export type ColorTokens = typeof colors;
export type ColorToken = keyof ColorTokens;
