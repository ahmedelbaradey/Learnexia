/**
 * useGetMyMissions — GET /api/Gamification/Missions/Me [Authorize]
 *
 * Returns all daily and weekly mission instances for the authenticated student's
 * current period. Each entry includes full mission state: code, titleKey, target,
 * progress, status, rewardXp, and period boundaries.
 *
 * Both lists may be empty for brand-new students who have not yet loaded the
 * dashboard (lazy instantiation of missions is triggered on first dashboard fetch).
 *
 * Hand-written hook (P4-08-FE-0): NSwag regen not yet available in CI shell.
 * Flows through the same ApiClient auth + envelope pipeline as all other hooks.
 *
 * Source: Learnexia.Modules.Gamification.Application/Features/Missions/Queries/GetMyMissions/
 *   MissionStateDto.cs, MyMissionsResponse.cs
 * BE controller: GamificationController → GET /api/Gamification/Missions/Me
 *
 * Query key: queryKeys.gamification.missions()
 * Replace with NSwag-generated method call once `pnpm gen:api` runs on a dev machine.
 */

import { useApiQuery, type UseApiQueryOptions } from './useApiQuery';
import { queryKeys } from '../query/queryKeys';
import type { MyMissionsResponse } from '../manual/gamification';

export function useGetMyMissions(
  options?: UseApiQueryOptions<MyMissionsResponse>,
) {
  return useApiQuery<MyMissionsResponse>(
    queryKeys.gamification.missions(),
    (client, signal) =>
      client.get<MyMissionsResponse>('/api/Gamification/Missions/Me', { signal }),
    options,
  );
}
