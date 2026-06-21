/**
 * FE-local DTOs for the parent analytics endpoints (P5-05).
 *
 * These shapes mirror the VERIFIED backend contracts (read directly from the
 * controller + DTO source in backend/src/Modules/Parent/). They are NOT
 * generated from Swagger — hand-typed to match the confirmed wire shapes.
 *
 * Base route: `api/Parent`. Auth: Bearer JWT (parent role). ChildId in path
 * (int). IDOR-guarded on the backend (IsParentOfChildAsync → generic 403).
 * Empty / first-week → 200 zero-state, never 404.
 *
 * Key constraints:
 *  - `WeeklyKpisDto.xpDelta` is ABSOLUTE XP (not a percent).
 *  - `SubjectMasteryResponseDto.bySubject[].subjectCode` is 0-indexed int:
 *    0=Math, 1=Science, 2=Arabic, 3=English (NOT 1-indexed).
 *  - `ChildReportsDto` feeds ALL three charts (daily-activity, 20-day trend,
 *    time-of-day) from a single GET /api/Parent/Children/{id}/Reports call.
 *  - `TimeOfDayBucketDto.bucket` is a named string "Morning|Afternoon|Evening|Night"
 *    (exactly 4 buckets — NOT 8 hourly bars with an `hour` integer).
 */

// ── E3 — Weekly KPIs (GET .../Children/{id}/WeeklyKpis) ──────────────────

/** Per-child weekly KPI aggregate + week-over-week deltas. */
export interface WeeklyKpisDto {
  /** Minutes of active learning this week. */
  timeLearningMinutes: number;
  /** XP earned this week. */
  xpEarned: number;
  /** Distinct lessons completed this week. */
  lessonsDone: number;
  /** Current consecutive-day learning streak. */
  streakDays: number;
  /** Change in time-learning minutes vs prior 7-day window. */
  timeLearningDeltaMinutes: number;
  /** XP earned this week minus XP earned the prior week (ABSOLUTE, not a percent). */
  xpDelta: number;
  /** Lessons completed this week minus lessons completed the prior week. */
  lessonsDelta: number;
  /** Streak delta (current minus prior week snapshot). */
  streakDelta: number;
}

// ── E6 — Reports charts (GET .../Children/{id}/Reports) ──────────────────
//   ONE endpoint feeds daily-activity (Shape A), 20-day trend (Shape B),
//   and time-of-day (Shape C). Do NOT split into separate calls.

/** One day's XP entry (used in dailyXpSeries and xpTrend20Day). */
export interface DailyXpEntryDto {
  /** UTC calendar date in ISO-8601 format ("yyyy-MM-dd"). */
  day: string;
  /** Total XP earned on this day (0 when no activity). */
  xp: number;
}

/** One of the 4 named time-of-day buckets (NOT hourly bars). */
export interface TimeOfDayBucketDto {
  /** One of: "Morning" | "Afternoon" | "Evening" | "Night". */
  bucket: string;
  /** Total XP earned in this time bucket. */
  totalXp: number;
}

/** E6 response — chart data for all three chart panels. */
export interface ChildReportsDto {
  /** Daily XP for each day in the rolling 7-day window (Mon–Sun). Guaranteed entries; zero on no-activity days. */
  dailyXpSeries: DailyXpEntryDto[];
  /** 20-day XP trend ending today. Exactly 20 entries; zero-filled on no-activity days. */
  xpTrend20Day: DailyXpEntryDto[];
  /**
   * XP aggregated by named time-of-day bucket.
   * Exactly 4 entries in order: Morning, Afternoon, Evening, Night.
   * Zero when bucket has no activity.
   */
  timeOfDayBuckets: TimeOfDayBucketDto[];
}

// ── E4 — Subject mastery (GET .../Children/{id}/SubjectMastery) ──────────

/** Per-subject mastery item. */
export interface SubjectMasteryItemDto {
  /**
   * Subject code as int (0-indexed): 0=Math, 1=Science, 2=Arabic, 3=English.
   * Note: NOT 1-indexed — use SUBJECT_CODE_MAP (not SUBJECT_ID_MAP).
   */
  subjectCode: number;
  /** Average mastery percentage for all skills in this subject (0–100). */
  percent: number;
}

/** E4 response — per-subject mastery breakdown. */
export interface SubjectMasteryResponseDto {
  /** Overall mastery percentage across all practiced subjects (0–100). */
  overallPercent: number;
  /**
   * Always 4 entries (Math / Science / Arabic / English) — zero for subjects
   * with no data yet.
   */
  bySubject: SubjectMasteryItemDto[];
}

// ── E5 — Weak areas (GET .../Children/{id}/WeakAreas) ────────────────────

/** One weak area item. */
export interface WeakAreaItemDto {
  skillId: number;
  skillName: string;
  /** Subject code as int (0=Math, 1=Science, 2=Arabic, 3=English). */
  subjectCode: number;
  masteryPercent: number;
  /** Severity: 1=Low, 2=Medium, 3=High. */
  severity: number;
  /** i18n key for the suggested next action (resolved by caller). */
  suggestedNextAction: string;
}

/** E5 response — focus/weak areas (empty list = no weak areas detected). */
export interface WeakAreasResponseDto {
  areas: WeakAreaItemDto[];
}

// ── E10 — Recommendations (GET .../Children/{id}/Recommendations) ────────

/** One recommendation item (all text fields are i18n keys). */
export interface RecommendationItemDto {
  /** Skill id targeted (0 = cold-start / generic). */
  skillId: number;
  /** Subject code as int (0=Math, 1=Science, 2=Arabic, 3=English). */
  subjectCode: number;
  /** i18n key for the recommendation title. */
  titleKey: string;
  /** i18n key for the recommendation body text. */
  bodyKey: string;
  /** i18n key for the call-to-action label. */
  ctaKey: string;
  /** Severity: 1=Low, 2=Medium, 3=High. */
  severity: number;
  /** Action type: 1=Practice, 2=Review, 3=KeepStreak, 4=Celebrate. */
  actionType: number;
  /** Target difficulty: 1=Easy, 2=Medium, 3=Hard. */
  targetDifficulty: number;
}

/** E10 response — daily recommendation set (empty list = cold-start / no data). */
export interface RecommendationsDto {
  items: RecommendationItemDto[];
}

// ── E1 — Child progress (GET .../Children/{id}/Progress) ────────────────

/** Weakest skill snapshot (null when no weak areas yet). */
export interface WeakSkillDto {
  skillId: number;
  skillName: string;
  /** Subject code as int (0=Math, 1=Science, 2=Arabic, 3=English). */
  subjectCode: number;
  masteryPercent: number;
  /** Severity: 1=Low, 2=Medium, 3=High. */
  severity: number;
}

/** Energy balance snapshot (zeroed when no billing data). */
export interface EnergySnapshotDto {
  remaining: number;
  allocated: number;
  spent: number;
  purchasedBalance: number;
  cycleEndsAtUtc: string | null;
}

/** E1 response — child-level progress card data. */
export interface ChildProgressDto {
  level: number;
  totalXp: number;
  currentStreak: number;
  bestStreak: number;
  masteryPercent: number;
  /** Null when the child has no weak areas yet. */
  weakestSkill: WeakSkillDto | null;
  activeToday: boolean;
  energy: EnergySnapshotDto;
}

// ── E2 — Family summary (GET .../Family/Summary) ────────────────────────

/** E2 response — "this week" combined family totals. */
export interface FamilySummaryDto {
  activeLearners: number;
  lessonsCompleted: number;
  totalXp: number;
  bestStreakDays: number;
  badgesEarned: number;
}

// ── E7 — Child energy (GET .../Children/{id}/Energy) ────────────────────

/** Per-helper-kind energy usage item. */
export interface EnergyUsageItemDto {
  /** Helper kind: one of hints, explain, deep, practice. */
  kind: string;
  count: number;
}

/** E7 response — energy balance and weekly-usage breakdown. */
export interface ChildEnergyDto {
  remaining: number;
  allocated: number;
  spent: number;
  purchasedBalance: number;
  cycleEndsAtUtc: string | null;
  /** Always exactly 4 entries (hints / explain / deep / practice). Zero counts when unused. */
  weeklyUsage: EnergyUsageItemDto[];
}

// ── Adapters ──────────────────────────────────────────────────────────────

/**
 * Maps the backend integer `subjectCode` (0-indexed) to the product subject key
 * used by the UI layer (`OVERVIEW_SUBJECT` / `SUBJECT_LABEL_KEY`).
 *
 * 0=Math, 1=Science, 2=Arabic, 3=English — confirmed from SubjectMasteryItemDto.
 * Unknown codes are silently dropped (not rendered).
 *
 * IMPORTANT: Field is `subjectCode` (not `subjectId`) and is 0-indexed (not 1-indexed).
 * The old `SUBJECT_ID_MAP` (1-indexed) was incorrect — this replaces it.
 */
export const SUBJECT_CODE_MAP: Record<number, 'Math' | 'Science' | 'Arabic' | 'English'> = {
  0: 'Math',
  1: 'Science',
  2: 'Arabic',
  3: 'English',
} as const;

/**
 * @deprecated Use `SUBJECT_CODE_MAP` instead. This alias is kept for backward
 * compatibility with existing consumers that import `SUBJECT_ID_MAP` while the
 * migration to the correct field name is in progress.
 * TODO: Remove once all consumers use `SUBJECT_CODE_MAP` and `subjectCode`.
 */
export const SUBJECT_ID_MAP = SUBJECT_CODE_MAP;
