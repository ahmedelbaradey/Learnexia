/**
 * useSetQuestionActive — PATCH /api/Learning/Questions/{id}/SetActive
 *
 * Activates or deactivates a question. Inactive questions are hidden from
 * students but remain visible in the admin list.
 * Body: { isActive: boolean }
 * Returns BaseResponse<string> (message). Auth: AdminOnly.
 *
 * Invalidates: adminCurriculum.questions(lessonId).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface SetQuestionActiveInput {
  id: number;
  lessonId: number;
  isActive: boolean;
}

export function useSetQuestionActive(): UseMutationResult<string, Error, SetQuestionActiveInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, SetQuestionActiveInput>({
    mutationFn: ({ id, isActive }) =>
      client.patch<string>(`/api/Learning/Questions/${id}/SetActive`, {
        body: { isActive },
      }),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminCurriculum.questions(variables.lessonId),
      });
    },
  });
}
