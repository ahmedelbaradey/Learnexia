/**
 * useUpdateSkill — PUT /api/learning/Skills/Update (AdminOnly).
 *
 * Edits an existing skill. Returns BaseResponse<string> (a success message).
 * Caller must refetch via invalidation — do NOT read new state from the result.
 *
 * Invalidates: adminCurriculum.skills() + adminCurriculum.skill(id) +
 * adminCurriculum.graph(subjectId) when a subjectId is provided.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditSkillDto } from './curriculum.types';

export interface UpdateSkillInput extends EditSkillDto {
  /** Optional: if provided, also invalidates the graph for this subject. */
  _subjectId?: number;
}

export function useUpdateSkill(): UseMutationResult<string, Error, UpdateSkillInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, UpdateSkillInput>({
    mutationFn: ({ _subjectId: _unused, ...body }) =>
      client.put<string>('/api/learning/Skills/Update', { body }),
    onSuccess: async (_data, { id, _subjectId }) => {
      const invalidations: Promise<void>[] = [
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.skills() }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.skill(id) }),
      ];
      if (_subjectId != null && _subjectId > 0) {
        invalidations.push(
          queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.graph(_subjectId) }),
        );
      }
      await Promise.all(invalidations);
    },
  });
}
