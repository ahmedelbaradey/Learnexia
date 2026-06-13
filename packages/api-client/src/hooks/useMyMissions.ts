/**
 * useMyMissions — GET /api/Gamification/Missions/Me [Authorize]
 *
 * Daily + weekly mission state for the current period, resolved from the JWT.
 * Returns `MyMissionsResponse` — `daily: MissionStateDto[]` +
 * `weekly: MissionStateDto[]` with `code`, `iconKey`, `titleKey`, `cadence`,
 * `target`, `rewardXp`, `progress`, `status` (MissionStatusDto), period bounds.
 * FE computes percent = `progress * 100 / target` (per-task contract).
 *
 * Weekly challenges are the `CHALLENGE_*`-coded rows in `weekly` — B8 filters
 * them client-side (Design Spec §8.3); B5 renders the rest.
 *
 * P4-08 carryover batch 2a (Design Spec §0.1). Consumed by the B5 missions
 * screen + dashboard widget and the B8 weekly-challenge cards (batch 3b).
 *
 * Operation id `missionsMe` is pinned by the Host NSwag `CustomOperationIds`
 * `/Me` fix (HANDOFF "Durable NSwag /Me fix") — do not rename.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { MyMissionsResponse } from '../schemas';

export function useMyMissions(
  options?: UseApiQueryOptions<MyMissionsResponse>,
): UseQueryResult<MyMissionsResponse, Error> {
  const client = useTypedClient();
  return useQuery<MyMissionsResponse, Error, MyMissionsResponse>({
    ...options,
    queryKey: queryKeys.gamification.missions(),
    queryFn: () => unwrapEnvelope(client.missionsMe()) as Promise<MyMissionsResponse>,
  });
}
