/**
 * useSignOutOtherSessions — POST /api/Users/Account/Sessions/SignOutOthers (authenticated).
 *
 * Wraps the NSwag `signOutOthers` method. Tells the BE to revoke all sessions
 * except the caller's current one. On success: invalidates `queryKeys.account.sessions()`
 * so the sessions list refetches and shows only the active (current) session.
 *
 * The caller remains logged in — BE does NOT revoke the current refresh token.
 */
import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';

export function useSignOutOtherSessions(): UseMutationResult<
  string | null | undefined,
  Error,
  void
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (): Promise<string | null | undefined> =>
      unwrapEnvelope(client.signOutOthers()) as Promise<string | null | undefined>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.account.sessions(),
      });
    },
  });
}
