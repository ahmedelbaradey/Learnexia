/**
 * useUpdateChildProfile — PATCH /api/Admin/Users/{childId}/profile (AdminOnly).
 *
 * Updates a child's harmless profile fields: preferredLanguage (UI locale)
 * and/or country. Both fields are optional — send only changed fields.
 *
 * This is NON-DESTRUCTIVE: no confirm gate, no progress impact.
 *
 * Backend contract:
 *   PATCH /api/Admin/Users/{childId}/profile
 *   Body: { preferredLanguage?: string, country?: string }
 *   Returns: BaseResponse<string> — message string only, NO DTO payload.
 *   → Refetch the profile via query invalidation; do NOT read new state from
 *     the mutation result.
 *
 * Field-name notes (EXACT — do not rename):
 *   - Body uses `preferredLanguage` (not `language`, not `uiLanguage`).
 *   - Body uses `country` (entity field is Nationality, DTO maps it as country).
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(childId)   → refreshes the detail page
 *   - queryKeys.adminUsers.all                → refreshes list (status may show)
 *
 * Error map:
 *   404 → child not found / not a child account
 *   422 → unsupported language or country > 100 chars
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface UpdateChildProfileInput {
  childId: number;
  preferredLanguage?: string;
  country?: string;
}

export function useUpdateChildProfile(): UseMutationResult<
  string,
  Error,
  UpdateChildProfileInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, UpdateChildProfileInput>({
    mutationFn: ({ childId, preferredLanguage, country }) => {
      // Only include defined fields — backend treats missing fields as no-change.
      const body: Record<string, string> = {};
      if (preferredLanguage !== undefined) body.preferredLanguage = preferredLanguage;
      if (country !== undefined) body.country = country;

      return client.patch<string>(
        `/api/Admin/Users/${childId}/profile`,
        { body },
      );
    },
    onSuccess: async (_data, { childId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminUsers.profile(childId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminUsers.all,
        }),
      ]);
    },
  });
}
