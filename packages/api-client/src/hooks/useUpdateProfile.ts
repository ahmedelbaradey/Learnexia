/**
 * useUpdateProfile — PUT /api/Users/Account/Profile (authenticated).
 *
 * Wraps the NSwag `profilePUT` method and unwraps to the typed
 * `AccountProfileResponse`. The acting account is resolved server-side from the
 * JWT — never sent in the body. On success it invalidates the account-profile
 * query and the `Me` query (which projects fullName / preferred language), so
 * both refetch the freshly-saved values. Drives the Settings → Profile tab's
 * Save action.
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { AccountProfileResponse, UpdateMyProfileCommand } from '../schemas';

export function useUpdateProfile(): UseMutationResult<
  AccountProfileResponse,
  Error,
  UpdateMyProfileCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      input: UpdateMyProfileCommand,
    ): Promise<AccountProfileResponse> =>
      unwrapEnvelope(client.profilePUT(input)) as Promise<AccountProfileResponse>,
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
