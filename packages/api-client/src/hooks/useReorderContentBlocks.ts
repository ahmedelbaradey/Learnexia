/**
 * useReorderContentBlocks — PUT /api/learning/ContentBlocks/Reorder (AdminOnly).
 *
 * Sends the full ordered content-block-id list.
 * All ids must belong to one lesson.
 * Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(lessonId), blocks(lessonId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReorderContentBlocksInput {
  lessonId: number;
  contentBlockIds: number[];
}

export function useReorderContentBlocks(): UseMutationResult<string, Error, ReorderContentBlocksInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReorderContentBlocksInput>({
    mutationFn: ({ contentBlockIds }) =>
      client.put<string>('/api/learning/ContentBlocks/Reorder', { body: { contentBlockIds } }),
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
