/**
 * useUpdateUserLanguage — PUT /api/Users/Account/Language (authenticated, self-scoped).
 *
 * Wraps the NSwag `language` method and unwraps the string response envelope.
 * Persists the authenticated user's `PreferredLanguage` (axis A — UI language)
 * to the backend so the choice survives sign-out/sign-in.
 *
 * Self-scoped: the user id is resolved server-side from the JWT — it is NOT sent
 * in the body. No IDOR risk. Previously this called the admin-only
 * UserManagement endpoint, which returned 403 for regular parents. The new
 * endpoint (`Account/Language`) is accessible to any authenticated user.
 *
 * On success it invalidates the `Me` query so the freshly-saved language is
 * reflected in the next `/Me` fetch. The caller is responsible for also
 * syncing the in-memory locale store (Zustand) + react-i18next — this hook
 * only handles the server-side persistence.
 *
 * No server data in Zustand — this is a mutation; the locale store holds the
 * currently-applied UI locale as a client/UI mirror only.
 */
import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UpdateMyPreferredLanguageCommand } from '../schemas';

export function useUpdateUserLanguage(): UseMutationResult<
  string | undefined,
  Error,
  UpdateMyPreferredLanguageCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      input: UpdateMyPreferredLanguageCommand,
    ): Promise<string | undefined> =>
      unwrapEnvelope(client.language(input)),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
    },
  });
}
