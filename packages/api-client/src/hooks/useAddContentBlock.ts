/**
 * useAddContentBlock — POST /api/learning/ContentBlocks (AdminOnly).
 *
 * Appends a new content block to a lesson.
 * Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(lessonId), adminCurriculum.blocks(lessonId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddContentBlockDto } from './curriculum.types';

export function useAddContentBlock(): UseMutationResult<string, Error, AddContentBlockDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddContentBlockDto>({
    mutationFn: (body) =>
      client.post<string>('/api/learning/ContentBlocks', { body }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.lesson(variables.lessonId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.blocks(variables.lessonId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
