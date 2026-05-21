/**
 * useUserProfile / useCurrentUser — query example.
 *
 * Pattern other queries follow: a stable query key from `queryKeys`, the client
 * unwrapping the envelope, and an AbortSignal passed through for cancellation.
 *
 * The 401-refresh and 422 handling all live in the client; the hook just maps
 * the endpoint to a key and surfaces `data`/`error`.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { UserProfileResponseDto } from '../schemas';

/** Fetch a user's profile by id. */
export function useUserProfile(
  userId: number | undefined,
): UseQueryResult<UserProfileResponseDto, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.users.profile(userId ?? -1),
    enabled: userId !== undefined && userId >= 0,
    queryFn: ({ signal }) =>
      client.get<UserProfileResponseDto>(
        '/api/Users/UserManagement/GetUserProfile',
        { query: { userId }, signal },
      ),
  });
}

/**
 * useCurrentUser — convenience wrapper that fetches the signed-in user's
 * profile. The app passes the current user id from `authStore`.
 */
export function useCurrentUser(
  currentUserId: number | undefined,
): UseQueryResult<UserProfileResponseDto, Error> {
  return useUserProfile(currentUserId);
}
