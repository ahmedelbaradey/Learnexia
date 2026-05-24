/**
 * useMyProfile — GET /api/Users/Account/Profile (authenticated).
 *
 * Wraps the NSwag `profileGET` method and unwraps to the typed
 * `AccountProfileResponse` (fullName / phone / country / avatarUrl). Self-scoped
 * — the account is resolved server-side from the JWT, no id parameter. Drives
 * the Settings → Profile tab's initial form values.
 *
 * Public signature: `useMyProfile(options?) => UseQueryResult<AccountProfileResponse, Error>`.
 * Pass `{ enabled: isSignedIn }` so it doesn't fire on the guest path.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { AccountProfileResponse } from '../schemas';

export function useMyProfile(
  options?: UseApiQueryOptions<AccountProfileResponse>,
): UseQueryResult<AccountProfileResponse, Error> {
  const client = useTypedClient();
  return useQuery<AccountProfileResponse, Error, AccountProfileResponse>({
    ...options,
    queryKey: queryKeys.account.profile(),
    queryFn: () =>
      unwrapEnvelope(client.profileGET()) as Promise<AccountProfileResponse>,
  });
}
