/**
 * useSuspendUser — POST /api/Admin/Users/{id}/suspend (AdminOnly).
 *
 * Transitions an Active account to Suspended. Revokes the user's Redis refresh
 * token and terminates all tracked sessions — sign-in is blocked going forward.
 *
 * RESIDUAL WINDOW (G2): an already-issued access JWT survives until its natural
 * expiry. The UI copy must NOT claim instant full logout — only that sign-in is
 * blocked and sessions are revoked.
 *
 * Backend contract:
 *   POST /api/Admin/Users/{id}/suspend
 *   Body: { reason: string } (required, max 500)
 *   Returns: BaseResponse<string> — a success MESSAGE string, NOT a profile DTO.
 *   → The hook output is string (the message). The caller must refetch the profile
 *     via query invalidation — do NOT read new status from the mutation result.
 *
 * Invalidations on success:
 *   - queryKeys.adminUsers.profile(id) — refreshes the detail page badge
 *   - queryKeys.adminUsers.all         — refreshes all list views
 *
 * Rejections (HTTP 400, successed=false):
 *   - Already Suspended
 *   - Target is Deleted (terminal)
 *   - Own account
 *   - SuperAdmin account
 *   404: user not found
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface SuspendUserInput {
  id: number;
  reason: string;
}

export function useSuspendUser(): UseMutationResult<string, Error, SuspendUserInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, SuspendUserInput>({
    mutationFn: ({ id, reason }) =>
      client.post<string>(`/api/Admin/Users/${id}/suspend`, { body: { reason } }),
    onSuccess: async (_data, { id }) => {
      // Invalidate single profile + all list caches so the new Suspended badge shows.
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.profile(id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminUsers.all }),
      ]);
    },
  });
}
