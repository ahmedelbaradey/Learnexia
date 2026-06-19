/**
 * useDeleteSkill — DELETE /api/learning/Skills?id={id} (AdminOnly).
 *
 * Soft-deletes a skill. Returns BaseResponse<string> (a success message).
 * Caller must refetch via invalidation — do NOT read new state from the result.
 *
 * Invalidates: adminCurriculum.skills() + adminCurriculum.skill(id) +
 * adminCurriculum.graph(subjectId) when a subjectId is provided.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteSkillInput {
  id: number;
  /** Optional: if provided, also invalidates the graph for this subject. */
  subjectId?: number;
}

export function useDeleteSkill(): UseMutationResult<string, Error, DeleteSkillInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteSkillInput>({
    mutationFn: ({ id }) =>
      client.delete<string>('/api/learning/Skills', { query: { id } }),
    onSuccess: async (_data, { id, subjectId }) => {
      const invalidations: Promise<void>[] = [
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.skills() }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.skill(id) }),
      ];
      if (subjectId != null && subjectId > 0) {
        invalidations.push(
          queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.graph(subjectId) }),
        );
      }
      await Promise.all(invalidations);
    },
  });
}
