/**
 * useUserActivity — GET /api/Admin/Users/{id}/activity (admin-only).
 *
 * Activity summary for a single user. Composed from Gamification seam queries;
 * each field is best-effort — a missing seam degrades to `null` for that field,
 * never to a 500. The component must render a "no data" state per null section.
 *
 * `lastSignInAtUtc` is ALWAYS null — no LastSignInAt column exists. Label in UI
 * as "Sign-in activity: not tracked" (decision D5, backend Q-A6 baked in).
 *
 * Backend contract (P7-06):
 *   GET /api/Admin/Users/{id}/activity
 *   Returns: BaseResponse<AdminActivitySummaryDto>
 *   Not found → 404. Each gamification section is best-effort (null on seam failure).
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 *
 * The family panel and activity panel load INDEPENDENTLY of the core profile.
 * A failing activity seam must not blank the profile (caller handles independently).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/Admin/AdminActivitySummaryDto.cs) ─

export interface AdminXpSummary {
  totalXp: number;
  currentLevel: number;
}

export interface AdminStreakSummary {
  currentStreak: number;
  longestStreak: number;
  /** ISO 8601 date string (DateOnly on the wire). Null if never active. */
  lastActivityDateUtc: string | null;
}

export interface AdminBadgesSummary {
  totalCount: number;
}

export interface AdminMissionsSummary {
  dailyMissionCount: number;
  completedDailyMissions: number;
}

export interface AdminLeagueSummary {
  tier: string;
  weeklyXp: number;
  currentRank: number;
  groupSize: number;
}

/**
 * Activity summary DTO for the admin activity endpoint.
 * Each gamification field is nullable — render "no data" per null section.
 */
export interface AdminActivitySummaryDto {
  userId: number;
  /**
   * Always null. Label in UI as "Sign-in activity: not tracked".
   * The backend Identity store has no LastSignInAt column.
   */
  lastSignInAtUtc: null;
  /** XP / level summary. Null when the gamification seam has no data. */
  xp: AdminXpSummary | null;
  /** Streak summary. Null when the gamification seam has no data. */
  streak: AdminStreakSummary | null;
  /** Badge count. Null when the gamification seam has no data. */
  badges: AdminBadgesSummary | null;
  /** Mission counts. Null when the gamification seam has no data. */
  missions: AdminMissionsSummary | null;
  /** League summary. Null when the gamification seam has no data. */
  league: AdminLeagueSummary | null;
}

// ── Hook ───────────────────────────────────────────────────────────────────────

export function useUserActivity(
  id: number,
): UseQueryResult<AdminActivitySummaryDto, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminUsers.activity(id),
    enabled: id > 0,
    queryFn: ({ signal }) =>
      client.get<AdminActivitySummaryDto>(
        `/api/Admin/Users/${id}/activity`,
        { signal },
      ),
  });
}
