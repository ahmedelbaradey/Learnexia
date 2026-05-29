/**
 * useResetPassword — POST /api/Users/Authentication/Reset-Password (anonymous).
 *
 * Wraps the NSwag `resetPassword` method. Takes `{ email, token, newPassword }`
 * (all three are required by the backend). Returns the server's string message
 * on success; on error the caller shows a localized error. The screen reads
 * `email` and `token` from URL search params (Expo Router `useLocalSearchParams`)
 * and the user types the new password.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { ResetPasswordCommand } from '../schemas';

export function useResetPassword(): UseMutationResult<
  string | undefined,
  Error,
  ResetPasswordCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (
      input: ResetPasswordCommand,
    ): Promise<string | undefined> =>
      unwrapEnvelope(client.resetPassword(input)) as Promise<
        string | undefined
      >,
  });
}
