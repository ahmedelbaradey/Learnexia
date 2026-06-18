/**
 * useDeleteContentBlock — DELETE /api/learning/ContentBlocks/{id} (AdminOnly).
 *
 * Soft-deletes a content block (IsDeleted=true). Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.lesson(lessonId), blocks(lessonId), all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteContentBlockInput {
  id: number;
  lessonId: number;
}

export function useDeleteContentBlock(): UseMutationResult<string, Error, DeleteContentBlockInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteContentBlockInput>({
    mutationFn: ({ id }) =>
      client.delete<string>(`/api/learning/ContentBlocks/${id}`),
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
