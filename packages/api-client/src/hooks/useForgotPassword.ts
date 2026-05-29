/**
 * useForgotPassword — POST /api/Users/Authentication/Forgot-Password (anonymous).
 *
 * Wraps the NSwag `forgotPassword` method. Takes `{ email }` and returns the
 * server's string message (which the FE ignores for anti-enumeration: the UI
 * always shows the same confirmation regardless of whether an account exists).
 * The server itself never reveals whether the email is registered (P1-13
 * anti-enumeration — any 200 or error gets the same "if an account exists…"
 * message in the UI).
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { ForgotPasswordCommand } from '../schemas';

export function useForgotPassword(): UseMutationResult<
  string | undefined,
  Error,
  ForgotPasswordCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (
      input: ForgotPasswordCommand,
    ): Promise<string | undefined> =>
      unwrapEnvelope(client.forgotPassword(input)) as Promise<
        string | undefined
      >,
  });
}
