/**
 * useDeleteUser — DELETE /api/Admin/Users/{id} (AdminOnly) — DELETE-with-body.
 *
 * Two-step confirm gate enforced by the backend: `confirm: false` returns HTTP
 * 424 (FailedDependency). In normal FE flow the final destructive button always
 * sends `confirm: true`, so 424 should not occur — but the hook surfaces it as
 * a typed ApiError with status 424 so the dialog can handle it defensively
 * (show a copy string rather than a generic error toast).
 *
 * SOFT-DELETE SEMANTICS (design decision D4):
 *   The row is retained. Learning history is preserved. PII is NOT yet anonymized
 *   (that is deferred to a later GDPR/COPPA sweep). The UI copy must reflect this
 *   accurately — never claim PII is erased.
 *
 * Backend contract:
 *   DELETE /api/Admin/Users/{id}
 *   Body: { reason: string, confirm: boolean, cascadeChildren: boolean }
 *   reason    — required, max 500.
 *   confirm   — must be true for the mutation to proceed (false → 424).
 *   cascadeChildren — only meaningful for Parent targets; ignored for children.
 *   Returns: BaseResponse<string> — a success MESSAGE string, NOT a profile DTO.
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(id) — refreshes the detail page (shows Deleted badge)
 *   - queryKeys.adminUsers.all         — refreshes all list views
 *
 * Rejections (HTTP 400, successed=false):
 *   - Already Deleted
 *   - Own account
 *   - SuperAdmin account
 *   404: user not found
 *   424: confirm: false (defensive; should not occur in normal FE flow)
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteUserInput {
  id: number;
  reason: string;
  /** Always true in the normal FE flow — the two-step UI only sends this after typed confirm. */
  confirm: boolean;
  /** Only meaningful when the target is a Parent. Defaults to false. */
  cascadeChildren: boolean;
}

export function useDeleteUser(): UseMutationResult<string, Error, DeleteUserInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteUserInput>({
    mutationFn: ({ id, reason, confirm, cascadeChildren }) =>
      client.delete<string>(`/api/Admin/Users/${id}`, {
        body: { reason, confirm, cascadeChildren },
      }),
    onSuccess: async (_data, { id }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.profile(id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.all }),
      ]);
    },
  });
}
