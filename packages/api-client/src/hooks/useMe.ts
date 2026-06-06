/**
 * useMe — GET /api/Users/Me (authenticated).
 *
 * Wraps the NSwag `me` method and unwraps to the typed `MeResponse`. Self-scoped
 * current-user projection that drives the routing guard: role, preferred
 * language, onboarding state (`isFirstLogin`), and `hasChildren`. No id
 * parameter — the user is resolved from the JWT.
 *
 * Public signature unchanged: `useMe(options?) => UseQueryResult<MeResponse, Error>`.
 * Pass `{ enabled: isSignedIn }` so it doesn't fire on the splash/guest path.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { MeResponse } from '../schemas';

export function useMe(
  options?: UseApiQueryOptions<MeResponse>,
): UseQueryResult<MeResponse, Error> {
  const client = useTypedClient();
  return useQuery<MeResponse, Error, MeResponse>({
    ...options,
    queryKey: queryKeys.auth.me(),
    queryFn: () => unwrapEnvelope(client.usersMe()) as Promise<MeResponse>,
  });
}
