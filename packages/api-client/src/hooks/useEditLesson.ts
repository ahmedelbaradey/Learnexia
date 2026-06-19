/**
 * useEditLesson — PUT /api/learning/Lessons/Update (AdminOnly).
 *
 * Updates lesson metadata. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(id), adminCurriculum.lessons(unitId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditLessonDto } from './curriculum.types';

export function useEditLesson(): UseMutationResult<string, Error, EditLessonDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, EditLessonDto>({
    mutationFn: (body) =>
      client.put<string>('/api/learning/Lessons/Update', { body }),
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
