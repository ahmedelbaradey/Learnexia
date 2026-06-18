/**
 * useOverrideChildGrade — POST /api/Admin/Users/{childId}/grade (AdminOnly).
 *
 * Overrides a child's grade (1–6). This is NON-DESTRUCTIVE: XP, level,
 * badges, streaks, and mastery records are preserved; the curriculum
 * re-scopes to the new grade.
 *
 * Backend contract:
 *   POST /api/Admin/Users/{childId}/grade
 *   Body: { grade: 1..6, reason?: string (≤500), confirm: boolean }
 *   Returns: BaseResponse<ChildGradeOverrideResponseDto>
 *            = { childUserId, oldGrade, newGrade }
 *
 * IMPORTANT field name: the confirm flag is `confirm` (NOT `confirmFreshStart`).
 *
 * Notable status codes:
 *   400 → confirm=false (soft guard) OR same grade as current
 *   422 → grade out of 1–6 range
 *   404 → child not found or not a Student account
 *
 * Design decision D3: reason is required by the FE (support-traceability)
 * even though the backend accepts null. The hook always receives a non-empty
 * reason when called via the dialog.
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(childId)   → refreshes grade on detail page
 *   - queryKeys.adminUsers.activity(childId)  → refreshes activity panel
 *   - queryKeys.adminUsers.all                → refreshes list
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ChildGradeOverrideResponseDto {
  childUserId: number;
  oldGrade: number;
  newGrade: number;
}

export interface OverrideChildGradeInput {
  childId: number;
  grade: number;
  reason?: string;
  /** Must be true — the dialog sets this only after the user has confirmed. */
  confirm: boolean;
}

export function useOverrideChildGrade(): UseMutationResult<
  ChildGradeOverrideResponseDto,
  Error,
  OverrideChildGradeInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<ChildGradeOverrideResponseDto, Error, OverrideChildGradeInput>({
    mutationFn: ({ childId, grade, reason, confirm }) =>
      client.post<ChildGradeOverrideResponseDto>(
        `/api/Admin/Users/${childId}/grade`,
        { body: { grade, reason, confirm } },
      ),
    onSuccess: async (_data, { childId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminUsers.profile(childId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminUsers.activity(childId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminUsers.all,
        }),
      ]);
    },
  });
}
