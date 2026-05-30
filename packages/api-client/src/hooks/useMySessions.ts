/**
 * useMySessions — GET /api/Users/Account/Sessions (authenticated).
 *
 * Wraps the NSwag `sessions` method and unwraps to `SessionInfo[]`.
 * Each item: `{ sessionId, isActive, isExpired, expiresAt, remainingSeconds }`.
 * No device / IP / user-agent metadata is available from the BE in W10 (P6-06).
 *
 * Security note: session IDs are NOT logged anywhere in this hook.
 * Callers render them truncated to the first 8 characters.
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { SessionInfo } from '../schemas';

export function useMySessions(
  options?: UseApiQueryOptions<SessionInfo[]>,
): UseQueryResult<SessionInfo[], Error> {
  const client = useTypedClient();
  return useQuery<SessionInfo[], Error, SessionInfo[]>({
    ...options,
    queryKey: queryKeys.account.sessions(),
    queryFn: () =>
      unwrapEnvelope(client.sessions()).then(
        (data) => (data ?? []) as SessionInfo[],
      ),
  });
}
