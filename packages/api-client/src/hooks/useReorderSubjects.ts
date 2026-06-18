/**
 * useReorderSubjects — PUT /api/learning/Subjects/Reorder (AdminOnly).
 *
 * Persists a new subject order for all subjects in the same (GradeId, Language)
 * tree. Array position = new sequenceOrder (0-based).
 *
 * Body: { subjectIds: number[] } — max 12 per request.
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.all (busts all subject lists).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReorderSubjectsInput {
  subjectIds: number[];
}

export function useReorderSubjects(): UseMutationResult<string, Error, ReorderSubjectsInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReorderSubjectsInput>({
    mutationFn: (body) =>
      client.put<string>('/api/learning/Subjects/Reorder', { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all });
    },
  });
}
