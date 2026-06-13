/**
 * useMyBadges — GET /api/Gamification/Badges/Me [Authorize]
 *
 * Full badge catalog (earned + locked) for the authenticated student, resolved
 * from the JWT. Returns `MyBadgesResponse` — `badges: BadgeStateDto[]` with
 * `code`, `iconKey`, `rarity` (BadgeRarity 1..4 → bronze/silver/gold/legendary),
 * `isEarned`, `rewardXp`, `awardedAtUtc`.
 *
 * P4-08 carryover batch 2a (Design Spec §0.1). Consumed by the B4 badge
 * collection screen + badge-earned popup (batch 3b).
 *
 * Operation id `badgesMe` is pinned by the Host NSwag `CustomOperationIds`
 * `/Me` fix (HANDOFF "Durable NSwag /Me fix") — do not rename.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { MyBadgesResponse } from '../schemas';

export function useMyBadges(
  options?: UseApiQueryOptions<MyBadgesResponse>,
): UseQueryResult<MyBadgesResponse, Error> {
  const client = useTypedClient();
  return useQuery<MyBadgesResponse, Error, MyBadgesResponse>({
    ...options,
    queryKey: queryKeys.gamification.badges(),
    queryFn: () => unwrapEnvelope(client.badgesMe()) as Promise<MyBadgesResponse>,
  });
}
