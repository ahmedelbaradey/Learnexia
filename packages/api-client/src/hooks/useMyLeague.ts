/**
 * useMyLeague — GET /api/Gamification/Leagues/Me [Authorize]  (note: PLURAL
 * "Leagues" — the P4-07 task file's `League/Me` is stale).
 *
 * Weekly-league snapshot for the authenticated student: `tier` (LeagueTierDto
 * 1..4 → bronze/silver/gold/diamond), `periodEndUtc` (week countdown),
 * `myRank`, `myWeeklyXp`, `standings: LeagueStandingDto[]` (server-anonymized
 * `displayName`, `isMe` flag), `promotionCutoffRank`, `demotionCutoffRank`.
 *
 * P4-08 carryover batch 2a (Design Spec §0.1). Consumed by the B6 league
 * standings screen (batch 3b).
 *
 * Operation id `leaguesMe` is pinned by the Host NSwag `CustomOperationIds`
 * `/Me` fix (HANDOFF "Durable NSwag /Me fix") — do not rename.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { MyLeagueResponse } from '../schemas';

export function useMyLeague(
  options?: UseApiQueryOptions<MyLeagueResponse>,
): UseQueryResult<MyLeagueResponse, Error> {
  const client = useTypedClient();
  return useQuery<MyLeagueResponse, Error, MyLeagueResponse>({
    ...options,
    queryKey: queryKeys.gamification.league(),
    queryFn: () => unwrapEnvelope(client.leaguesMe()) as Promise<MyLeagueResponse>,
  });
}
