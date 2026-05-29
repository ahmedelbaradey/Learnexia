/**
 * useRemoveAvatar — DELETE /api/Users/Account/Avatar (authenticated).
 *
 * Wraps the NSwag `avatarDELETE` method. Returns the `AvatarUploadResponse`
 * (which will have an empty/null `avatarUrl` post-removal). On success
 * invalidates myProfile + the Me query so both refresh and the avatar reverts
 * to initials. Drives the Settings → Profile avatar Remove button.
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { AvatarUploadResponse } from '../schemas';

export function useRemoveAvatar(): UseMutationResult<
  AvatarUploadResponse,
  Error,
  void
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (): Promise<AvatarUploadResponse> =>
      unwrapEnvelope(client.avatarDELETE()) as Promise<AvatarUploadResponse>,
    onSuccess: async () => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.account.profile(),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() }),
      ]);
    },
  });
}
