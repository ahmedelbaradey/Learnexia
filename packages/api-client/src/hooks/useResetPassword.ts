/**
 * useResetPassword — POST /api/Users/Authentication/Reset-Password (anonymous).
 *
 * Wraps the NSwag `resetPassword(ResetPasswordCommand)` method. The transport
 * recognizes the Reset-Password path as anonymous (added to
 * ANONYMOUS_PATH_SUFFIXES in apiClient.ts — FE-0a).
 *
 * Security notes:
 *  - The reset `token` is NEVER logged, placed in an error message, rendered in
 *    a component key, or leaked into aria-labels. It travels only in the
 *    mutation body to the backend.
 *  - Password values are NEVER logged.
 *  - BE is authoritative for password-policy enforcement; client-side validation
 *    is UX only.
 *  - 400/410/422 responses with hints containing "token"/"expired"/"invalid"
 *    should be mapped to dedicated i18n copy on the calling screen — never echo
 *    the raw backend message.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { ResetPasswordCommand } from '../schemas';

export function useResetPassword(): UseMutationResult<
  string | null | undefined,
  Error,
  ResetPasswordCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (
      input: ResetPasswordCommand,
    ): Promise<string | null | undefined> =>
      unwrapEnvelope(client.resetPassword(input)) as Promise<
        string | null | undefined
      >,
  });
}
