/**
 * Hand-written TypeScript DTOs for the four Gamification endpoints.
 *
 * These types are manually authored to match the live BE DTOs exactly.
 * They are NOT generated — a proper NSwag regen should replace this file
 * in a follow-up PR once the dev machine toolchain is available.
 *
 * Source of truth (BE files):
 *  - Profile:  Learnexia.Modules.Gamification.Application/Features/Profile/Dtos/StudentProfileDto.cs
 *  - Badges:   .../Features/Badges/Queries/GetMyBadges/{BadgeStateDto,MyBadgesResponse}.cs
 *  - Missions: .../Features/Missions/Queries/GetMyMissions/{MissionStateDto,MyMissionsResponse}.cs
 *  - Leagues:  .../Features/Leagues/Queries/GetMyLeague/{LeagueStandingDto,MyLeagueResponse}.cs
 *  - Enums:    Learnexia.Shared.Contracts.Gamification (cross-module mirrors)
 *              Learnexia.Modules.Gamification.Domain.Enums (original domain values)
 *
 * Casing convention: PascalCase on BE → camelCase on TS via System.Text.Json default.
 *
 * P4-08 HANDOFF NOTE:
 *   Run `pnpm gen:api` on a dev machine against `GET /swagger/v1/swagger.json`
 *   to replace this file with NSwag-generated code. Until then, update here when
 *   the BE DTO contracts change.
 */

// ---------------------------------------------------------------------------
// Profile  — GET /api/Gamification/Profile
// Source: StudentProfileDto.cs
// ---------------------------------------------------------------------------

/**
 * XP profile snapshot for the authenticated student.
 * All level math is pre-computed by the BE query handler via LevelCurve.
 */
export interface StudentProfileDto {
  /** Total accumulated XP across all time. */
  totalXp: number;
  /** Current XP level (1-based). */
  currentLevel: number;
  /** XP earned within the current level (0 ≤ xpIntoCurrentLevel < xpToNextLevel). */
  xpIntoCurrentLevel: number;
  /** XP required to advance from the current level to the next. */
  xpToNextLevel: number;
}

// ---------------------------------------------------------------------------
// Badges  — GET /api/Gamification/Badges/Me
// Source: BadgeStateDto.cs, MyBadgesResponse.cs, BadgeRarity enum, BadgeTriggerType enum
// ---------------------------------------------------------------------------

/**
 * Mirror of Learnexia.Modules.Gamification.Domain.Enums.BadgeRarity.
 * Integer values: Common=1, Rare=2, Epic=3, Legendary=4.
 * System.Text.Json serializes enums as their name string by default in the BE
 * (JsonStringEnumConverter is registered in the API Program.cs).
 */
export type BadgeRarity = 'Common' | 'Rare' | 'Epic' | 'Legendary';

/**
 * Mirror of Learnexia.Modules.Gamification.Domain.Enums.BadgeTriggerType.
 * Integer values: FirstLesson=1, StreakThreshold=2, LevelThreshold=3.
 */
export type BadgeTriggerType = 'FirstLesson' | 'StreakThreshold' | 'LevelThreshold';

/**
 * Single badge definition annotated with the student's earned state.
 * Source: BadgeStateDto.cs (P4-05, AC4).
 * Ordered by Rarity ASC, SortOrder ASC — stable order for FE grid rendering.
 */
export interface BadgeStateDto {
  /** Stable badge identifier (e.g., "FIRST_LESSON"). */
  code: string;
  /** FE asset bundle key (e.g., "icon.first_lesson"). */
  iconKey: string;
  /** Badge rarity tier. */
  rarity: BadgeRarity;
  /** What event triggers this badge award. */
  triggerType: BadgeTriggerType;
  /**
   * Numeric threshold for StreakThreshold / LevelThreshold trigger types.
   * Null for FirstLesson trigger (no threshold required).
   */
  threshold: number | null;
  /** XP rewarded when this badge is earned. */
  rewardXp: number;
  /** True when the student has earned this badge. */
  isEarned: boolean;
  /** ISO 8601 UTC timestamp of award, or null when not yet earned. */
  awardedAtUtc: string | null;
}

/** Response envelope for GET /api/Gamification/Badges/Me. */
export interface MyBadgesResponse {
  /** Full badge catalog with each entry's earned state for the authenticated student. */
  badges: BadgeStateDto[];
}

// ---------------------------------------------------------------------------
// Missions  — GET /api/Gamification/Missions/Me
// Source: MissionStateDto.cs, MyMissionsResponse.cs
//         MissionTypeDto, MissionStatusDto, MissionTargetTypeDto enums
//         (from Learnexia.Shared.Contracts.Gamification)
// ---------------------------------------------------------------------------

/**
 * Mission cadence. Mirror of Learnexia.Shared.Contracts.Gamification.MissionTypeDto.
 * Integer values: Daily=1, Weekly=2.
 */
export type MissionCadence = 'Daily' | 'Weekly';

/**
 * Mission lifecycle state. Mirror of Learnexia.Shared.Contracts.Gamification.MissionStatusDto.
 * Integer values: NotStarted=0, InProgress=1, Completed=2, Expired=3.
 */
export type MissionStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Expired';

/**
 * What the student must do to make progress.
 * Mirror of Learnexia.Shared.Contracts.Gamification.MissionTargetTypeDto.
 * Integer values: CompleteLessons=1, CorrectAnswers=2, MaintainStreak=3.
 */
export type MissionTargetType = 'CompleteLessons' | 'CorrectAnswers' | 'MaintainStreak';

/**
 * Full mission state for the GET /api/Gamification/Missions/Me endpoint.
 * Richer than the cross-module MissionSummary used in the dashboard preview —
 * exposes period boundaries and rewardXp for the Missions screen (P4-08).
 * Source: MissionStateDto.cs (P4-06).
 */
export interface MissionStateDto {
  /** Stable mission code (e.g., "DAILY_3_LESSONS"). */
  code: string;
  /** FE asset bundle key (e.g., "icon.mission.daily_3_lessons"). */
  iconKey: string;
  /** FE i18n title key (e.g., "mission.daily_3_lessons.title"). */
  titleKey: string;
  /** Daily or Weekly cadence. Corresponds to BE field "Cadence" (MissionTypeDto). */
  cadence: MissionCadence;
  /** What the student must do to progress. */
  targetType: MissionTargetType;
  /** Target integer (completion threshold). */
  target: number;
  /** XP rewarded upon mission completion. */
  rewardXp: number;
  /** Current progress integer (0 ≤ progress ≤ target). */
  progress: number;
  /** Mission lifecycle state. */
  status: MissionStatus;
  /** ISO 8601 UTC — start of the current period (day for Daily, week for Weekly). */
  periodStartUtc: string;
  /** ISO 8601 UTC — end of the current period (exclusive). */
  periodEndUtc: string;
  /** ISO 8601 UTC — when the mission was completed, or null when not yet completed. */
  completedAtUtc: string | null;
}

/** Response envelope for GET /api/Gamification/Missions/Me. */
export interface MyMissionsResponse {
  /** All daily mission instances for the current period, ordered by SortOrder ASC. */
  daily: MissionStateDto[];
  /** All weekly mission instances for the current period, ordered by SortOrder ASC. */
  weekly: MissionStateDto[];
}

// ---------------------------------------------------------------------------
// Leagues  — GET /api/Gamification/Leagues/Me
// Source: LeagueStandingDto.cs, MyLeagueResponse.cs
//         LeagueTierDto enum (from Learnexia.Shared.Contracts.Gamification)
// ---------------------------------------------------------------------------

/**
 * League tier. Mirror of Learnexia.Shared.Contracts.Gamification.LeagueTierDto.
 * Integer values: Bronze=1, Silver=2, Gold=3, Diamond=4 (D6 locked decision).
 */
export type LeagueTier = 'Bronze' | 'Silver' | 'Gold' | 'Diamond';

/**
 * Single ranked entry in the league standings list (P4-07, AC3).
 * Display names are always anonymized as "Student #N" — no PII exposed (D2).
 * Source: LeagueStandingDto.cs.
 */
export interface LeagueStandingDto {
  /** 1-based rank within the cohort, ordered WeeklyXp DESC / JoinedAtUtc ASC. */
  rank: number;
  /**
   * Anonymized display name: "Student #N" where N is the stable JoinOrder.
   * Never contains a real name or username (child privacy — D2).
   */
  displayName: string;
  /** XP earned in the current week's competition period. */
  weeklyXp: number;
  /** True when this entry corresponds to the authenticated caller. */
  isMe: boolean;
}

/**
 * Full league view response for GET /api/Gamification/Leagues/Me.
 * Source: MyLeagueResponse.cs (P4-07, AC3).
 */
export interface MyLeagueResponse {
  /** The student's current league tier. */
  tier: LeagueTier;
  /** ISO week key — e.g. "W:2026-23". */
  periodKey: string;
  /** ISO 8601 UTC — Monday 00:00 UTC, start of this week's competition period. */
  periodStartUtc: string;
  /** ISO 8601 UTC — next Monday 00:00 UTC, exclusive end of this week's competition period. */
  periodEndUtc: string;
  /** The caller's current 1-based rank (0 = brand-new student with no membership). */
  myRank: number;
  /** The caller's weekly XP in this period. */
  myWeeklyXp: number;
  /** Full sorted standings (up to 30 entries). */
  standings: LeagueStandingDto[];
  /** Top-N rank threshold: members ranked ≤ this are promoted. 0 when Diamond tier. */
  promotionCutoffRank: number;
  /** Bottom-N rank threshold: members ranked ≥ this are demoted. 0 when Bronze tier. */
  demotionCutoffRank: number;
}

// ---------------------------------------------------------------------------
// Dashboard supplement — types for DashboardDto fields added in P4-05..P4-07
// that are missing from the generated nswag-client.ts snapshot.
// Source: Learnexia.Shared.Contracts.Gamification.{BadgeSummary,MissionSummary}
//         Learning module GetDashboard handler for the dashboard DTO assembly.
// ---------------------------------------------------------------------------

/**
 * Summary of a single earned badge used in the dashboard "recent badges" strip.
 * Source: BadgeSummary.cs in Learnexia.Shared.Contracts.Gamification (P4-05, D2).
 * Limited to top 3 most recently earned badges, ordered AwardedAtUtc DESC.
 */
export interface BadgeSummary {
  /** Stable badge code (e.g., "FIRST_LESSON"). */
  code: string;
  /** FE asset bundle key (e.g., "icon.first_lesson"). */
  iconKey: string;
  /** Badge rarity tier. */
  rarity: BadgeRarity;
  /** ISO 8601 UTC timestamp of award. */
  awardedAtUtc: string;
}

/**
 * Summary of a single mission instance used in the dashboard preview.
 * Source: MissionSummary.cs in Learnexia.Shared.Contracts.Gamification (P4-06).
 * Smaller than MissionStateDto — no period boundaries or rewardXp.
 */
export interface MissionSummary {
  /** Stable mission code (e.g., "DAILY_3_LESSONS"). */
  code: string;
  /** FE asset bundle key. */
  iconKey: string;
  /** FE i18n title key. */
  titleKey: string;
  /** What the student must do to progress. */
  targetType: MissionTargetType;
  /** Current progress integer. */
  progress: number;
  /** Target integer (completion threshold). */
  target: number;
  /** Mission lifecycle state. */
  status: MissionStatus;
  /** ISO 8601 UTC when completed, or null when not yet completed. */
  completedAtUtc: string | null;
}
