/**
 * useChangePassword — POST /api/Users/Account/ChangePassword (authenticated).
 *
 * Wraps the NSwag `changePassword` method targeting `/api/Users/Account/ChangePassword`.
 * DO NOT confuse with `changePasswordForUser` (legacy admin route
 * `/api/Users/UserManagement/ChangePasswordForUser`) — that is a different method.
 *
 * The BE invalidates all OTHER sessions and revokes the refresh-token cache on
 * success; the caller's active session keeps working. The FE does not log out the
 * current user after a successful password change — it clears the form and shows
 * a success strip noting that other sessions have been signed out.
 *
 * Security notes:
 *  - The password is never logged (no console.log in this file or the calling panel).
 *  - The mutation key does NOT include the password value.
 *  - No TanStack Query invalidation needed (password change has no cached data to refresh).
 */
import {
  useMutation,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { ChangePasswordCommand } from '../schemas';

export function useChangePassword(): UseMutationResult<
  string | null | undefined,
  Error,
  ChangePasswordCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: ChangePasswordCommand): Promise<string | null | undefined> =>
      unwrapEnvelope(client.changePassword(input)) as Promise<string | null | undefined>,
    // No invalidation — password change does not affect any cached query data.
    // The calling panel (SecurityPanel) manually invalidates useMySessions after
    // success because the BE kills other sessions.
  });
}
