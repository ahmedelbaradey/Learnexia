/**
 * useCreateSkill — POST /api/learning/Skills/Create (AdminOnly).
 *
 * Creates a new skill. Returns BaseResponse<string> (a success message).
 * Caller must refetch via invalidation — do NOT read new state from the result.
 *
 * Invalidates: adminCurriculum.skills() + adminCurriculum.graph(subjectId) when
 * a subjectId is provided (the new skill may have a node in the current tree).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddSkillDto } from './curriculum.types';

export interface CreateSkillInput extends AddSkillDto {
  /** Optional: if provided, also invalidates the graph for this subject. */
  _subjectId?: number;
}

export function useCreateSkill(): UseMutationResult<string, Error, CreateSkillInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, CreateSkillInput>({
    mutationFn: ({ _subjectId: _unused, ...body }) =>
      client.post<string>('/api/learning/Skills/Create', { body }),
    onSuccess: async (_data, { _subjectId }) => {
      const invalidations: Promise<void>[] = [
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.skills() }),
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
