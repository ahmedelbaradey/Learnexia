/**
 * useDashboard — GET /api/Learning/Dashboard [Authorize]
 *
 * Per-student dashboard resolved from the JWT. Returns the full DashboardDto
 * in one call (Continue Card, XP, Streak, DailyMission, LeaguePreview).
 *
 * Phase-2 contract: xp=0, streak=0, dailyMission=null, leaguePreview=null.
 * `continue` is REAL in Phase 2 — the BE resolves the most-recent-attempt
 * subject → engine → first Available lesson (Grade-1 Math fallback for
 * net-new students). FE reads it directly; no client-side heuristic needed.
 *
 * Query-key seam (`queryKeys.learning.dashboard()`) was already added in W12
 * as a forward-compat placeholder — reused here.
 *
 * Future invalidation: fire `queryClient.invalidateQueries(queryKeys.learning.dashboard())`
 * on `LessonCompletedIntegrationEvent` or its FE equivalent (P4-02 wave).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { DashboardDto } from '../schemas';

export function useDashboard(
  options?: UseApiQueryOptions<DashboardDto>,
): UseQueryResult<DashboardDto, Error> {
  const client = useTypedClient();
  return useQuery<DashboardDto, Error, DashboardDto>({
    ...options,
    queryKey: queryKeys.learning.dashboard(),
    queryFn: () => unwrapEnvelope(client.dashboard()) as Promise<DashboardDto>,
  });
}
