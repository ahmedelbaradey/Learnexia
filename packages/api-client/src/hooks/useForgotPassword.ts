/**
 * useForgotPassword — POST /api/Users/Authentication/Forgot-Password (anonymous).
 *
 * Wraps the NSwag `forgotPassword(ForgotPasswordCommand)` method. The transport
 * recognizes the Forgot-Password path as anonymous (added to
 * ANONYMOUS_PATH_SUFFIXES in apiClient.ts — FE-0a).
 *
 * ANTI-ENUMERATION CONTRACT: The calling screen MUST show a generic
 * "If an account exists for that email…" confirmation on ANY 2xx response,
 * and must never branch the confirmation copy on whether the account exists.
 * Only non-enumeration failures (network/5xx) should show an error banner.
 *
 * No token handling in this hook (it is anonymous and returns a plain string).
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { ForgotPasswordCommand } from '../schemas';

export function useForgotPassword(): UseMutationResult<
  string | null | undefined,
  Error,
  ForgotPasswordCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (
      input: ForgotPasswordCommand,
    ): Promise<string | null | undefined> =>
      unwrapEnvelope(client.forgotPassword(input)) as Promise<
        string | null | undefined
      >,
  });
}
