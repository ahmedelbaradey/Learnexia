/**
 * useDeleteLesson — DELETE /api/learning/Lessons?id={id} (AdminOnly).
 *
 * Soft-deletes a lesson (and its content blocks atomically).
 * Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lessons(unitId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteLessonInput {
  id: number;
  unitId: number;
}

export function useDeleteLesson(): UseMutationResult<string, Error, DeleteLessonInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteLessonInput>({
    mutationFn: ({ id }) =>
      client.delete<string>(`/api/learning/Lessons`, { query: { id } }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.lessons(variables.unitId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
