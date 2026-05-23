/**
 * Parent-dashboard STUB data — TODO(P5).
 *
 * Per-child XP / level / streak / mastery / weakest topic and the family
 * "this week" totals are NOT available from any endpoint yet (analytics is
 * Phase 5). The screen is built layout-first against the real children list
 * (`useMyChildren`, P1-04) with these clearly-marked stubs so the layout is
 * complete and ready to swap for real data when the Phase-5 reports endpoints
 * land. Do NOT fake an API for these — they are deterministic placeholders
 * derived from the child id so each card is stable and visually distinct.
 *
 * `weakestTopicKey` references a fixed, product-subject-aligned topic set
 * (enum-style const) rather than a free string.
 */

/** Fixed weakest-topic set (placeholder; real topics come from Phase-5). */
export const WEAKEST_TOPIC = {
  Fractions: 'fractions',
  Letters: 'letters',
  Geometry: 'geometry',
  Reading: 'reading',
  Numbers: 'numbers',
} as const;

export type WeakestTopicKey = (typeof WEAKEST_TOPIC)[keyof typeof WEAKEST_TOPIC];

const WEAKEST_TOPIC_KEYS = Object.values(WEAKEST_TOPIC);

/** Per-child stub stats (TODO(P5): replace with the reports endpoint). */
export interface ChildStatsStub {
  grade: number;
  level: number;
  xp: number;
  streakDays: number;
  masteryPercent: number;
  weakestTopicKey: WeakestTopicKey;
  activeToday: boolean;
  /** Locale label key the child learns in (placeholder until profile enrich). */
  locale: 'ar' | 'en';
}

/** Family "this week" combined totals (TODO(P5)). */
export interface FamilyTotalsStub {
  activeLearners: number;
  lessonsCompleted: number;
  totalXp: number;
  bestStreakDays: number;
  badgesEarned: number;
}

/** Deterministic hash so the same child id always yields the same stub. */
function hash(seed: string): number {
  let h = 0;
  for (let i = 0; i < seed.length; i += 1) h = (h * 31 + seed.charCodeAt(i)) >>> 0;
  return h;
}

/**
 * TODO(P5): replace with real per-child analytics. Deterministic placeholder
 * derived from the child id (stable across renders, distinct per child).
 */
export function getChildStatsStub(childId: string): ChildStatsStub {
  const h = hash(childId);
  return {
    grade: (h % 6) + 1,
    level: (h % 20) + 1,
    xp: ((h % 40) + 1) * 100,
    streakDays: h % 12,
    masteryPercent: 40 + (h % 55),
    weakestTopicKey: WEAKEST_TOPIC_KEYS[h % WEAKEST_TOPIC_KEYS.length] ?? WEAKEST_TOPIC.Numbers,
    activeToday: h % 3 !== 0,
    locale: h % 2 === 0 ? 'en' : 'ar',
  };
}

/** TODO(P5): replace with the real combined family totals. */
export function getFamilyTotalsStub(childIds: string[]): FamilyTotalsStub {
  const stats = childIds.map(getChildStatsStub);
  return {
    activeLearners: stats.filter((s) => s.activeToday).length,
    lessonsCompleted: stats.reduce((sum, s) => sum + (s.level % 9), 0),
    totalXp: stats.reduce((sum, s) => sum + s.xp, 0),
    bestStreakDays: stats.reduce((max, s) => Math.max(max, s.streakDays), 0),
    badgesEarned: stats.reduce((sum, s) => sum + (s.level % 4), 0),
  };
}
