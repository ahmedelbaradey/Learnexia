/**
 * useEditContentBlock — PUT /api/learning/ContentBlocks (AdminOnly).
 *
 * Updates a content block's type and payload. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(lessonId), blocks(lessonId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditContentBlockDto } from './curriculum.types';

export interface EditContentBlockInput extends EditContentBlockDto {
  lessonId: number;
}

export function useEditContentBlock(): UseMutationResult<string, Error, EditContentBlockInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, EditContentBlockInput>({
    mutationFn: ({ id, blockType, payload }) =>
      client.put<string>('/api/learning/ContentBlocks', { body: { id, blockType, payload } }),
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
