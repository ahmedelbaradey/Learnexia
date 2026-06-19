/**
 * useDeleteQuestion — DELETE /api/Learning/Questions/{id}
 *
 * Soft-deletes a question (IsDeleted=true; global filter hides from students).
 * Returns BaseResponse<string> (message). Auth: AdminOnly.
 *
 * Invalidates: adminCurriculum.questions(lessonId).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteQuestionInput {
  id: number;
  lessonId: number;
}

export function useDeleteQuestion(): UseMutationResult<string, Error, DeleteQuestionInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteQuestionInput>({
    mutationFn: ({ id }) =>
      client.delete<string>(`/api/Learning/Questions/${id}`),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminCurriculum.questions(variables.lessonId),
      });
    },
  });
}
