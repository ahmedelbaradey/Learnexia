/**
 * useGetMyProfile — GET /api/Gamification/Profile [Authorize]
 *
 * Returns the authenticated student's XP profile snapshot:
 * total XP, current level, XP into the current level, and XP required to
 * reach the next level. All level math is pre-computed by the BE via LevelCurve.
 *
 * Hand-written hook (P4-08-FE-0): the NSwag-generated client does not yet
 * include the Gamification endpoints. This hook uses `useApiQuery` + `client.get`
 * directly, flowing through the same auth + 401-refresh + envelope-unwrap
 * pipeline as every other hook via `ApiClient.get<T>()`.
 *
 * Source: Learnexia.Modules.Gamification.Application/Features/Profile/Dtos/StudentProfileDto.cs
 * BE controller: GamificationController → GET /api/Gamification/Profile
 *
 * Query key: queryKeys.gamification.profile()
 * Replace with NSwag-generated method call once `pnpm gen:api` runs on a dev machine.
 */

import { useApiQuery, type UseApiQueryOptions } from './useApiQuery';
import { queryKeys } from '../query/queryKeys';
import type { StudentProfileDto } from '../manual/gamification';

export function useGetMyProfile(
  options?: UseApiQueryOptions<StudentProfileDto>,
) {
  return useApiQuery<StudentProfileDto>(
    queryKeys.gamification.profile(),
    (client, signal) =>
      client.get<StudentProfileDto>('/api/Gamification/Profile', { signal }),
    options,
  );
}
