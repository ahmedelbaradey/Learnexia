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
  /**
   * Helper-Energy balance (0–300). DISPLAY-ONLY stub — the real balance lands
   * with the energy backend (Batch D / P-energy). Does NOT imply a working
   * balance beyond a number; the mini-stat is non-interactive.
   */
  energy: number;
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
    // TODO(Batch D / P-energy): replace with the real Helper-Energy balance.
    energy: h % 301,
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

/* ------------------------------------------------------------------ */
/* Overview screen stubs (P1-11-FE-8) — TODO(P5-05).                   */
/* The Overview "this week" KPIs, per-subject mastery and focus areas   */
/* have no endpoint yet (analytics = Phase 5). Deterministic per-child  */
/* placeholders so the layout is complete and stable per child.        */
/* ------------------------------------------------------------------ */

/** The 4 product subjects (no Social Studies) — drives the mastery rows. */
export const OVERVIEW_SUBJECT = {
  Math: 'Math',
  Science: 'Science',
  Arabic: 'Arabic',
  English: 'English',
} as const;

export type OverviewSubjectKey = (typeof OVERVIEW_SUBJECT)[keyof typeof OVERVIEW_SUBJECT];

const OVERVIEW_SUBJECT_KEYS = Object.values(OVERVIEW_SUBJECT);

/** Per-child "this week" overview KPIs (TODO(P5): reports endpoint). */
export interface OverviewKpiStub {
  /** Minutes learning this week. */
  timeLearningMinutes: number;
  /** Minutes gained vs last week (positive = improvement). */
  timeLearningDeltaMinutes: number;
  xpEarned: number;
  /** Percent change vs last week. */
  xpDeltaPercent: number;
  lessonsDone: number;
  lessonsDelta: number;
  streakDays: number;
  streakDelta: number;
}

/** One subject-mastery row (TODO(P5)). */
export interface SubjectMasteryStub {
  subject: OverviewSubjectKey;
  /** Mastery percentage 0..100. */
  percent: number;
}

/** Fixed focus-area severity (drives the bar tint), never a raw string. */
export const FOCUS_SEVERITY = {
  High: 'high',
  Medium: 'medium',
} as const;

export type FocusSeverity = (typeof FOCUS_SEVERITY)[keyof typeof FOCUS_SEVERITY];

/** One "areas to focus on" row (TODO(P5)). */
export interface FocusAreaStub {
  topicKey: WeakestTopicKey;
  subject: OverviewSubjectKey;
  /** Confidence percentage 0..100. */
  percent: number;
  severity: FocusSeverity;
}

/** TODO(P5-05): replace with the real per-child weekly KPIs. */
export function getOverviewKpiStub(childId: string): OverviewKpiStub {
  const h = hash(childId);
  return {
    timeLearningMinutes: 120 + (h % 180),
    timeLearningDeltaMinutes: 10 + (h % 50),
    xpEarned: ((h % 9) + 1) * 60,
    xpDeltaPercent: 5 + (h % 35),
    lessonsDone: (h % 18) + 2,
    lessonsDelta: (h % 5) + 1,
    streakDays: h % 12,
    streakDelta: (h % 2) + 1,
  };
}

/** TODO(P5-05): replace with real per-subject mastery (4 product subjects). */
export function getSubjectMasteryStub(childId: string): SubjectMasteryStub[] {
  const h = hash(childId);
  return OVERVIEW_SUBJECT_KEYS.map((subject, i) => ({
    subject,
    percent: 45 + ((h + i * 17) % 50),
  }));
}

/** TODO(P5-05): replace with real focus areas (weakest topics). */
export function getFocusAreasStub(childId: string): FocusAreaStub[] {
  const h = hash(childId);
  const rows: FocusAreaStub[] = [
    {
      topicKey: WEAKEST_TOPIC.Fractions,
      subject: OVERVIEW_SUBJECT.Math,
      percent: 35 + (h % 15),
      severity: FOCUS_SEVERITY.High,
    },
    {
      topicKey: WEAKEST_TOPIC.Letters,
      subject: OVERVIEW_SUBJECT.Arabic,
      percent: 50 + (h % 15),
      severity: FOCUS_SEVERITY.Medium,
    },
    {
      topicKey: WEAKEST_TOPIC.Geometry,
      subject: OVERVIEW_SUBJECT.Science,
      percent: 55 + (h % 12),
      severity: FOCUS_SEVERITY.Medium,
    },
  ];
  return rows;
}
