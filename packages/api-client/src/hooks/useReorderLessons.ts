/**
 * useReorderLessons — PUT /api/learning/Lessons/Reorder (AdminOnly).
 *
 * Sends the full ordered lesson-id list (position = new sequenceOrder).
 * All ids must belong to one unit.
 * Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lessons(unitId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReorderLessonsInput {
  unitId: number;
  lessonIds: number[];
}

export function useReorderLessons(): UseMutationResult<string, Error, ReorderLessonsInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReorderLessonsInput>({
    mutationFn: ({ lessonIds }) =>
      client.put<string>('/api/learning/Lessons/Reorder', { body: { lessonIds } }),
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
