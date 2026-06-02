/**
 * useGetMyBadges — GET /api/Gamification/Badges/Me [Authorize]
 *
 * Returns the full badge catalog (10 definitions) annotated with the
 * authenticated student's earned state. Ordered Rarity ASC, SortOrder ASC
 * by the BE — stable order for FE grid rendering.
 *
 * Earned badges: isEarned=true, awardedAtUtc=ISO timestamp.
 * Locked badges: isEarned=false, awardedAtUtc=null.
 *
 * Hand-written hook (P4-08-FE-0): NSwag regen not yet available in CI shell.
 * Flows through the same ApiClient auth + envelope pipeline as all other hooks.
 *
 * Source: Learnexia.Modules.Gamification.Application/Features/Badges/Queries/GetMyBadges/
 *   BadgeStateDto.cs, MyBadgesResponse.cs
 * BE controller: GamificationController → GET /api/Gamification/Badges/Me
 *
 * Query key: queryKeys.gamification.badges()
 * Replace with NSwag-generated method call once `pnpm gen:api` runs on a dev machine.
 */

import { useApiQuery, type UseApiQueryOptions } from './useApiQuery';
import { queryKeys } from '../query/queryKeys';
import type { MyBadgesResponse } from '../manual/gamification';

export function useGetMyBadges(
  options?: UseApiQueryOptions<MyBadgesResponse>,
) {
  return useApiQuery<MyBadgesResponse>(
    queryKeys.gamification.badges(),
    (client, signal) =>
      client.get<MyBadgesResponse>('/api/Gamification/Badges/Me', { signal }),
    options,
  );
}
