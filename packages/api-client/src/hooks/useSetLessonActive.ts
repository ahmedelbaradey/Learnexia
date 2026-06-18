/**
 * useSetLessonActive — PUT /api/learning/Lessons/{id}/Active (AdminOnly).
 *
 * Activates or deactivates a lesson. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(id), adminCurriculum.lessons(unitId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface SetLessonActiveInput {
  id: number;
  unitId: number;
  isActive: boolean;
}

export function useSetLessonActive(): UseMutationResult<string, Error, SetLessonActiveInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, SetLessonActiveInput>({
    mutationFn: ({ id, isActive }) =>
      client.put<string>(`/api/learning/Lessons/${id}/Active`, { body: { lessonId: id, isActive } }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.lesson(variables.id),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.lessons(variables.unitId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
