/**
 * useReactivateUser — POST /api/Admin/Users/{id}/reactivate (AdminOnly).
 *
 * Transitions a Suspended account back to Active. The user will need to sign in
 * fresh to receive a new session — no tokens are issued automatically.
 *
 * Backend contract:
 *   POST /api/Admin/Users/{id}/reactivate
 *   Body: { reason?: string | null } (reason is OPTIONAL)
 *   Returns: BaseResponse<string> — a success MESSAGE string, NOT a profile DTO.
 *   → The hook output is string (the message). The caller must refetch the profile
 *     via query invalidation — do NOT read new status from the mutation result.
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(id) — refreshes the detail page badge
 *   - queryKeys.adminUsers.all         — refreshes all list views
 *
 * Rejections (HTTP 400, successed=false):
 *   - Already Active
 *   - Target is Deleted (terminal — cannot reactivate)
 *   404: user not found
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReactivateUserInput {
  id: number;
  /** Optional reactivation reason. Pass null or omit to send no reason. */
  reason?: string | null;
}

export function useReactivateUser(): UseMutationResult<string, Error, ReactivateUserInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReactivateUserInput>({
    mutationFn: ({ id, reason }) =>
      client.post<string>(`/api/Admin/Users/${id}/reactivate`, {
        body: { reason: reason ?? null },
      }),
    onSuccess: async (_data, { id }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.profile(id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.all }),
      ]);
    },
  });
}
