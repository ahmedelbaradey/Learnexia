/**
 * useRemoveAvatar — DELETE /api/Users/Account/Avatar (authenticated).
 *
 * Wraps the NSwag `avatarDELETE()` method. On success it invalidates the
 * account-profile query and the `Me` query so the `Avatar` component falls back
 * to initials automatically.
 *
 * No confirm dialog — the danger-styled "Remove" button + reversibility (re-upload
 * at any time) is the UX safeguard (per design spec / lead decision).
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
