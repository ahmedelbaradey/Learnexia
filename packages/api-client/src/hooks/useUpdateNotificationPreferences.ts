/**
 * useUpdateNotificationPreferences — PUT /api/Notifications/Preferences (authenticated).
 *
 * Wraps the NSwag `preferencesPUT` method. The BE validator requires ALL 4
 * categories, distinct — partial PATCH semantics are not supported. Callers
 * must always pass the full array (4 items).
 *
 * On success: invalidates `queryKeys.notifications.preferences()` so the GET
 * query refreshes. Callers may additionally do optimistic updates on the cache
 * before firing this mutation (see NotificationsPanel for the pattern).
 */
import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UpdateMyNotificationPreferencesCommand } from '../schemas';

export function useUpdateNotificationPreferences(): UseMutationResult<
  string | null | undefined,
  Error,
  UpdateMyNotificationPreferencesCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      input: UpdateMyNotificationPreferencesCommand,
    ): Promise<string | null | undefined> =>
      unwrapEnvelope(client.preferencesPUT(input)) as Promise<string | null | undefined>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.notifications.preferences(),
      });
    },
  });
}
