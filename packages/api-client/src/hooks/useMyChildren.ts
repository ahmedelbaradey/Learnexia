/**
 * useMyChildren — GET /api/Users/Parent/My-Children (authenticated).
 *
 * Wraps the NSwag `myChildren` method and unwraps to the typed
 * `LinkedChildResponse[]`. Scoped server-side to the JWT user — no parent id
 * parameter. Used by the My-Children switcher (Design Spec Screen 8) and
 * invalidated after Add-Child / Link-Child.
 *
 * Public signature unchanged.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { LinkedChildResponse } from '../schemas';

export function useMyChildren(
  options?: UseApiQueryOptions<LinkedChildResponse[]>,
): UseQueryResult<LinkedChildResponse[], Error> {
  const client = useTypedClient();
  return useQuery<LinkedChildResponse[], Error, LinkedChildResponse[]>({
    ...options,
    queryKey: queryKeys.family.myChildren(),
    queryFn: () =>
      unwrapEnvelope(client.myChildren()).then(
        (data) => (data ?? []) as LinkedChildResponse[],
      ),
  });
}
