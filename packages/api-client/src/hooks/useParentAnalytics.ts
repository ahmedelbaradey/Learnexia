/**
 * Parent analytics query hooks (P5-05).
 *
 * All hooks follow the `useApiClient().get<T>()` pattern (raw client, not NSwag
 * generated — analytics routes are not in the stale generated client).
 *
 * Base route: `api/Parent`. PascalCase route segments. ChildId in path (int).
 * Auth: Bearer JWT from signed-in parent (resolved server-side; no extra header).
 * IDOR guard on server: non-owned child → 403 (NEVER 404 — must not leak ids).
 * Empty / first-week → 200 zero-state on every endpoint.
 *
 * KEY DESIGN DECISION: The three chart panels (daily-activity, 20-day trend,
 * time-of-day) are served by ONE endpoint: GET /api/Parent/Children/{id}/Reports.
 * Use `useChildReports(childId)` for all three — do NOT call separate endpoints.
 *
 * Stale time: 2 minutes — analytics data is relatively stable within a session.
 *
 * Correct endpoints (PascalCase, base "api/Parent"):
 *   GET api/Parent/Children/{id}/WeeklyKpis
 *   GET api/Parent/Children/{id}/Reports          ← feeds all 3 charts
 *   GET api/Parent/Children/{id}/SubjectMastery
 *   GET api/Parent/Children/{id}/WeakAreas
 *   GET api/Parent/Children/{id}/Recommendations
 *   GET api/Parent/Children/{id}/Progress
 *   GET api/Parent/Children/{id}/Energy
 *   GET api/Parent/Children/{id}/Activity
 *   GET api/Parent/Family/Summary
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type {
  WeeklyKpisDto,
  ChildReportsDto,
  SubjectMasteryResponseDto,
  WeakAreasResponseDto,
  RecommendationsDto,
  ChildProgressDto,
  FamilySummaryDto,
  ChildEnergyDto,
} from './parentAnalytics.types';

const STALE_2_MIN = 2 * 60 * 1000;

/**
 * Weekly KPI aggregate for a child (E3).
 * Disabled when `childId` is falsy (no active child selected).
 */
export function useWeeklyKpis(
  childId: string | undefined,
): UseQueryResult<WeeklyKpisDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.weeklyKpis(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<WeeklyKpisDto>(
        `api/Parent/Children/${childId}/WeeklyKpis`,
        { signal },
      ),
  });
}

/**
 * Combined reports data for a child (E6) — feeds ALL three chart panels:
 *   - `dailyXpSeries`    → daily-activity bar chart (Shape A, 7 bars)
 *   - `xpTrend20Day`     → 20-day XP trend (Shape B, exactly 20 bars)
 *   - `timeOfDayBuckets` → time-of-day distribution (Shape C, 4 named buckets)
 *
 * Call this ONCE; destructure the result for each chart panel. Do NOT call
 * separate endpoints for the three charts — they do not exist.
 *
 * Disabled when `childId` is falsy.
 */
export function useChildReports(
  childId: string | undefined,
): UseQueryResult<ChildReportsDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.reports(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<ChildReportsDto>(
        `api/Parent/Children/${childId}/Reports`,
        { signal },
      ),
  });
}

/**
 * Per-subject mastery percentages for a child (E4).
 * `bySubject` always contains 4 entries (Math/Science/Arabic/English).
 * `subjectCode` is 0-indexed (0=Math, 1=Science, 2=Arabic, 3=English).
 * Disabled when `childId` is falsy.
 */
export function useSubjectMastery(
  childId: string | undefined,
): UseQueryResult<SubjectMasteryResponseDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.subjectMastery(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<SubjectMasteryResponseDto>(
        `api/Parent/Children/${childId}/SubjectMastery`,
        { signal },
      ),
  });
}

/**
 * Weak areas / focus areas for a child (E5).
 * Empty `areas` list = no weak areas detected (not an error).
 * Disabled when `childId` is falsy.
 */
export function useWeakAreas(
  childId: string | undefined,
): UseQueryResult<WeakAreasResponseDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.weakAreas(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<WeakAreasResponseDto>(
        `api/Parent/Children/${childId}/WeakAreas`,
        { signal },
      ),
  });
}

/**
 * Daily recommendation set for a child (E10).
 * Empty `items` list = cold-start / no weak areas / job not yet run (not an error).
 * All text fields in items are i18n keys — resolved by the FE.
 * Disabled when `childId` is falsy.
 */
export function useChildRecommendations(
  childId: string | undefined,
): UseQueryResult<RecommendationsDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.recommendations(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<RecommendationsDto>(
        `api/Parent/Children/${childId}/Recommendations`,
        { signal },
      ),
  });
}

// Deferred: P5-05 design spec scopes Progress/Energy out; wired in a later pass.
/**
 * Child-level progress card data (E1).
 * Feeds `ChildDashboardCard` / `MyChildren` (replaces `getChildStatsStub`).
 * `weakestSkill` is null when no weak areas yet.
 * Disabled when `childId` is falsy.
 */
export function useChildProgress(
  childId: string | undefined,
): UseQueryResult<ChildProgressDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.childProgress(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<ChildProgressDto>(
        `api/Parent/Children/${childId}/Progress`,
        { signal },
      ),
  });
}

/**
 * Family "this week" combined summary (E2).
 * Feeds `FamilySummaryStrip` (replaces `getFamilyTotalsStub`).
 * Not keyed by child — returns data across all children in the family.
 */
export function useFamilySummary(): UseQueryResult<FamilySummaryDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.familySummary(),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<FamilySummaryDto>(
        'api/Parent/Family/Summary',
        { signal },
      ),
  });
}

// Deferred: P5-05 design spec scopes Progress/Energy out; wired in a later pass.
/**
 * Child energy balance and weekly-usage breakdown (E7).
 * Feeds `EnergyWeb` (replaces energy stubs).
 * `weeklyUsage` always contains exactly 4 entries.
 * Disabled when `childId` is falsy.
 */
export function useChildEnergy(
  childId: string | undefined,
): UseQueryResult<ChildEnergyDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.parentAnalytics.childEnergy(childId ?? ''),
    enabled: Boolean(childId),
    staleTime: STALE_2_MIN,
    queryFn: ({ signal }) =>
      client.get<ChildEnergyDto>(
        `api/Parent/Children/${childId}/Energy`,
        { signal },
      ),
  });
}
