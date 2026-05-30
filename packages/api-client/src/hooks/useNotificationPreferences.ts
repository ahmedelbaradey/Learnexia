/**
 * useNotificationPreferences — GET /api/Notifications/Preferences (authenticated).
 *
 * Wraps the NSwag `preferencesGET` method and unwraps to
 * `NotificationPreferencesResponse` (an object with a `preferences` array of
 * 4 `NotificationPreferenceItemDto` items). Drives the Settings → Notifications
 * tab's initial toggle state.
 *
 * Numeric `category` values map to:
 *   0 = WeeklyReport, 1 = StreakAtRisk, 2 = ProductAnnouncement, 3 = Achievement
 * (NotificationCategory enum from the generated client).
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { NotificationPreferencesResponse } from '../schemas';

export function useNotificationPreferences(
  options?: UseApiQueryOptions<NotificationPreferencesResponse>,
): UseQueryResult<NotificationPreferencesResponse, Error> {
  const client = useTypedClient();
  return useQuery<NotificationPreferencesResponse, Error, NotificationPreferencesResponse>({
    ...options,
    queryKey: queryKeys.notifications.preferences(),
    queryFn: () =>
      unwrapEnvelope(client.preferencesGET()) as Promise<NotificationPreferencesResponse>,
  });
}
