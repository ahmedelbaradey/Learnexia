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

/* ================================================================== */
/* Batch D — Helper Energy screen stubs.                              */
/*                                                                    */
/* A Phase-10 billing/energy-ledger BACKEND exists on `main`, but it  */
/* has NO api-client hooks yet (no generated query/mutation seam).    */
/* So balance + weekly usage are DISPLAY-ONLY deterministic stubs.    */
/* When the energy api-client lands, swap getEnergyBalanceStub /      */
/* getEnergyUsageStub for the real query and the top-up pack for the  */
/* real IAP product (see energy.tsx IAP TODO).                        */
/* ================================================================== */

/** Helper-Energy balance + reset window (DISPLAY-ONLY stub). */
export interface EnergyBalanceStub {
  /** Energy credits remaining this period (⚡). */
  balance: number;
  /** Monthly allowance cap (the "/ 300" denominator). */
  cap: number;
  /** Days until the monthly allowance resets. */
  resetsInDays: number;
  /** Per-day spend cap (e.g. 20/day). */
  dailyCap: number;
}

/** One weekly-usage tile: an AI-helper kind + how many times it was used. */
export interface EnergyUsageStub {
  /** Stable, non-localized key (drives icon/color + the i18n label). */
  kind: 'hints' | 'explain' | 'deep' | 'practice';
  /** Times this helper was used this week. */
  count: number;
}

/** One purchasable top-up pack (IAP product placeholder — NOT wired). */
export interface EnergyTopUpPackStub {
  /** Stable product id (maps to a store product when IAP is wired). */
  id: string;
  /** Credits granted (⚡). */
  credits: number;
  /** Display price string — Latin technical string, NOT localized digits. */
  priceLabel: string;
}

/**
 * DISPLAY-ONLY balance stub. Deterministic per parent/family seed so the
 * battery is stable across renders. TODO(Batch D / P10 energy api-client):
 * replace with the real energy-ledger balance query once hooks are generated.
 */
export function getEnergyBalanceStub(seed = 'family'): EnergyBalanceStub {
  const h = hash(seed);
  return {
    // Mirrors the design preview (180 / 300) but stays deterministic.
    balance: 120 + (h % 121),
    cap: 300,
    resetsInDays: 5 + (h % 20),
    dailyCap: 20,
  };
}

/**
 * DISPLAY-ONLY weekly-usage stub (deterministic). TODO(Batch D / P10): swap
 * for the real per-helper usage breakdown from the energy-ledger api-client.
 */
export function getEnergyUsageStub(seed = 'family'): EnergyUsageStub[] {
  const h = hash(seed);
  return [
    { kind: 'hints', count: 24 + (h % 24) },
    { kind: 'explain', count: 6 + (h % 12) },
    { kind: 'deep', count: 2 + (h % 8) },
    { kind: 'practice', count: 1 + (h % 6) },
  ];
}

/**
 * Top-up pack catalogue (stub). IAP IS GATED — these are display placeholders
 * only; the "Buy" CTA is a coming-soon stub (see energy.tsx). TODO(Batch D /
 * P10 IAP): replace with real store products + a payments-backed purchase flow.
 */
export function getEnergyTopUpPacksStub(): EnergyTopUpPackStub[] {
  return [{ id: 'energy-500', credits: 500, priceLabel: '$2.99' }];
}

/* ================================================================== */
/* Batch D — Activity timeline stubs.                                 */
/*                                                                    */
/* No activity/notification endpoint exists yet. These are typed,     */
/* deterministic placeholders so the timeline + filter chips are      */
/* fully exercisable. TODO(P-activity): replace with the real         */
/* activity-feed query when the endpoint lands.                       */
/* ================================================================== */

/** Activity event category — also the filter-chip taxonomy. */
export const ACTIVITY_CATEGORY = {
  Badge: 'badge',
  Energy: 'energy',
  Alert: 'alert',
} as const;

export type ActivityCategory =
  (typeof ACTIVITY_CATEGORY)[keyof typeof ACTIVITY_CATEGORY];

/** Specific event kind (drives icon/color + the i18n message). */
export const ACTIVITY_KIND = {
  BadgeEarned: 'badgeEarned',
  LevelUp: 'levelUp',
  LessonCompleted: 'lessonCompleted',
  EnergyUsed: 'energyUsed',
  EnergyLow: 'energyLow',
  StreakReached: 'streakReached',
  Inactive: 'inactive',
} as const;

export type ActivityKind = (typeof ACTIVITY_KIND)[keyof typeof ACTIVITY_KIND];

/** Which filter chip an event kind belongs to. */
const ACTIVITY_KIND_CATEGORY: Record<ActivityKind, ActivityCategory> = {
  [ACTIVITY_KIND.BadgeEarned]: ACTIVITY_CATEGORY.Badge,
  [ACTIVITY_KIND.LevelUp]: ACTIVITY_CATEGORY.Badge,
  [ACTIVITY_KIND.LessonCompleted]: ACTIVITY_CATEGORY.Badge,
  [ACTIVITY_KIND.EnergyUsed]: ACTIVITY_CATEGORY.Energy,
  [ACTIVITY_KIND.EnergyLow]: ACTIVITY_CATEGORY.Energy,
  [ACTIVITY_KIND.StreakReached]: ACTIVITY_CATEGORY.Alert,
  [ACTIVITY_KIND.Inactive]: ACTIVITY_CATEGORY.Alert,
};

export function categoryForKind(kind: ActivityKind): ActivityCategory {
  return ACTIVITY_KIND_CATEGORY[kind];
}

/** One timeline event (typed, deterministic stub). */
export interface ActivityEventStub {
  id: string;
  kind: ActivityKind;
  category: ActivityCategory;
  /** Child display name (who the event is about). */
  childName: string;
  /** Minutes ago this happened (drives the relative-time label). */
  minutesAgo: number;
  /**
   * Optional numeric detail used by the message (e.g. streak days, lesson
   * score, helpers used). Localized at render via Intl.
   */
  amount?: number;
}

/**
 * Deterministic activity-event feed (stub). Mixed types across the three
 * filter categories so every chip resolves to ≥1 event AND the empty state is
 * reachable by combining filters in tests. TODO(P-activity): replace with the
 * real activity endpoint.
 */
export function getActivityEventsStub(): ActivityEventStub[] {
  const make = (
    id: string,
    kind: ActivityKind,
    childName: string,
    minutesAgo: number,
    amount?: number,
  ): ActivityEventStub => ({
    id,
    kind,
    category: categoryForKind(kind),
    childName,
    minutesAgo,
    amount,
  });
  return [
    make('a1', ACTIVITY_KIND.BadgeEarned, 'Sami', 2),
    make('a2', ACTIVITY_KIND.LessonCompleted, 'Layla', 40, 5),
    make('a3', ACTIVITY_KIND.EnergyUsed, 'Sami', 60, 3),
    make('a4', ACTIVITY_KIND.StreakReached, 'Sami', 180, 7),
    make('a5', ACTIVITY_KIND.EnergyLow, 'Yusuf', 600, 12),
    make('a6', ACTIVITY_KIND.Inactive, 'Yusuf', 1440, 2),
    make('a7', ACTIVITY_KIND.LevelUp, 'Layla', 1500, 4),
  ];
}
