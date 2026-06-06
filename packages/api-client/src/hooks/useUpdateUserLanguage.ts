/**
 * useUpdateUserLanguage — PUT /api/Users/UserManagement/UpdateUserLanguage (authenticated).
 *
 * Wraps the NSwag `updateUserLanguage` method and unwraps the void response.
 * Persists the authenticated user's `PreferredLanguage` (axis A — UI language)
 * to the backend so the choice survives sign-out/sign-in.
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
import { queryKeys } from '../query/queryKeys';
import type { EditUserPreferredLanguageCommand } from '../schemas';

export function useUpdateUserLanguage(): UseMutationResult<
  void,
  Error,
  EditUserPreferredLanguageCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: EditUserPreferredLanguageCommand): Promise<void> =>
      client.updateUserLanguage(input),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() });
    },
  });
}
