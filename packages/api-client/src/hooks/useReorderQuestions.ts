/**
 * useReorderQuestions — PUT /api/Learning/Questions/Reorder
 *
 * Batch-reorders questions within a lesson. The position index in the array
 * becomes the new SequenceOrder for each question.
 * Returns BaseResponse<string> (message). Auth: AdminOnly.
 *
 * Invalidates: adminCurriculum.questions(lessonId).
 *
 * NOTE: The backend audit log records TargetEntityId: 0 for Reorder (batch op).
 * This is a known BE limitation; no FE action required.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReorderQuestionsInput {
  lessonId: number;
  questionIds: number[];
}

export function useReorderQuestions(): UseMutationResult<string, Error, ReorderQuestionsInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReorderQuestionsInput>({
    mutationFn: (payload) =>
      client.put<string>('/api/Learning/Questions/Reorder', { body: payload }),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminCurriculum.questions(variables.lessonId),
      });
    },
  });
}
