/**
 * useCreateLesson — POST /api/learning/Lessons/Create (AdminOnly).
 *
 * Creates a lesson under a unit. Returns BaseResponse<string> (success message).
 * Invalidates: adminCurriculum.lessons(unitId) + adminCurriculum.all.
 * NO optimistic update — invalidate-and-refetch per Wave-1 pattern.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddLessonDto } from './curriculum.types';

export function useCreateLesson(): UseMutationResult<string, Error, AddLessonDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddLessonDto>({
    mutationFn: (body) =>
      client.post<string>('/api/learning/Lessons/Create', { body }),
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
