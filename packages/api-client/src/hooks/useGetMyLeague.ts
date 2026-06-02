/**
 * useGetMyLeague — GET /api/Gamification/Leagues/Me [Authorize]
 *
 * Returns the authenticated student's full league view for the current week:
 * - Current tier (Bronze / Silver / Gold / Diamond).
 * - The student's own rank and weekly XP.
 * - Up to 30 anonymized standings rows ("Student #N" — no PII, child-safe).
 * - Week boundary timestamps (periodStartUtc / periodEndUtc).
 * - Promotion and demotion cutoff ranks (used to render zone dividers on the League screen).
 *
 * For brand-new students with no league membership, a sentinel snapshot is returned
 * (tier: Bronze, myRank: 0, myWeeklyXp: 0, standings: []).
 *
 * Hand-written hook (P4-08-FE-0): NSwag regen not yet available in CI shell.
 * Flows through the same ApiClient auth + envelope pipeline as all other hooks.
 *
 * Source: Learnexia.Modules.Gamification.Application/Features/Leagues/Queries/GetMyLeague/
 *   LeagueStandingDto.cs, MyLeagueResponse.cs
 * BE controller: GamificationController → GET /api/Gamification/Leagues/Me
 *
 * Query key: queryKeys.gamification.league()
 * Replace with NSwag-generated method call once `pnpm gen:api` runs on a dev machine.
 */

import { useApiQuery, type UseApiQueryOptions } from './useApiQuery';
import { queryKeys } from '../query/queryKeys';
import type { MyLeagueResponse } from '../manual/gamification';

export function useGetMyLeague(
  options?: UseApiQueryOptions<MyLeagueResponse>,
) {
  return useApiQuery<MyLeagueResponse>(
    queryKeys.gamification.league(),
    (client, signal) =>
      client.get<MyLeagueResponse>('/api/Gamification/Leagues/Me', { signal }),
    options,
  );
}
