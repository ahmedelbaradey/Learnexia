/**
 * useAdminChangeChildLearningLanguage — POST /api/Admin/Users/{childId}/learning-language
 * (AdminOnly).
 *
 * ADMIN-ONLY variant of the learning-language change. This is DISTINCT from the
 * parent-app `useChangeLearningLanguage` hook (which targets
 * PUT /api/Parent/Change-Learning-Language and uses the `newLearningLanguage` body
 * field). Do NOT confuse or reuse the parent hook here.
 *
 * This action is DESTRUCTIVE:
 *   - Hard-deletes the child's Math and Science lesson attempts.
 *   - Cannot be undone.
 *   - Arabic and English subject data are NOT affected.
 *
 * Backend contract:
 *   POST /api/Admin/Users/{childId}/learning-language
 *   Body: { learningLanguage: "ar" | "en", confirmFreshStart: boolean }
 *   Returns: BaseResponse<AdminChangedLearningLanguageResponseDto>
 *            = { childId, newLearningLanguage }
 *
 * IMPORTANT field names (do NOT guess):
 *   - Body key: `learningLanguage` (NOT `newLearningLanguage` — that is the parent
 *     endpoint's key on a DIFFERENT route).
 *   - Confirm key: `confirmFreshStart` (NOT `confirm` — the grade endpoint uses
 *     `confirm`; this endpoint uses `confirmFreshStart`).
 *
 * Notable status codes:
 *   424 → confirmFreshStart=false — no mutation was performed (gating fired server-
 *          side before any change); the FE maps this to the gate message.
 *   422 → unsupported language (not "ar" or "en")
 *   200 → same language as current → no-op success (backend handles gracefully)
 *   404 → child not found or not a Student account
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(childId)   → refreshes learningLanguage on detail
 *   - queryKeys.adminUsers.activity(childId)  → Math/Science progress is gone;
 *                                               activity panel must reload
 *   - queryKeys.adminUsers.all                → refreshes list
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface AdminChangedLearningLanguageResponseDto {
  childId: number;
  newLearningLanguage: string;
}

export interface AdminChangeChildLearningLanguageInput {
  childId: number;
  /** Must be "ar" or "en". */
  learningLanguage: 'ar' | 'en';
  /**
   * Must be true — the dialog sets this only after the user has typed "CONFIRM".
   * If false, the backend returns 424 (FailedDependency) without mutating.
   */
  confirmFreshStart: boolean;
}

export function useAdminChangeChildLearningLanguage(): UseMutationResult<
  AdminChangedLearningLanguageResponseDto,
  Error,
  AdminChangeChildLearningLanguageInput
> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<
    AdminChangedLearningLanguageResponseDto,
    Error,
    AdminChangeChildLearningLanguageInput
  >({
    mutationFn: ({ childId, learningLanguage, confirmFreshStart }) =>
      client.post<AdminChangedLearningLanguageResponseDto>(
        `/api/Admin/Users/${childId}/learning-language`,
        { body: { learningLanguage, confirmFreshStart } },
      ),
    onSuccess: async (_data, { childId }) => {
      // Invalidate profile (learningLanguage field), activity (Math/Science
      // progress is hard-deleted), and all-lists.
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
