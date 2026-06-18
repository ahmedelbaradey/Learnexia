/**
 * useAddQuestion — POST /api/Learning/Questions
 *
 * Creates a new question attached to a lesson. Returns BaseResponse<string> (message).
 * Appends to the end of the lesson's sequence.
 * Auth: AdminOnly.
 *
 * Invalidates: adminCurriculum.questions(lessonId).
 * No optimistic update — invalidate-and-refetch (Wave-1 decision D8).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddQuestionPayload } from './curriculum.types';

export function useAddQuestion(): UseMutationResult<string, Error, AddQuestionPayload> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddQuestionPayload>({
    mutationFn: (payload) =>
      client.post<string>('/api/Learning/Questions', { body: payload }),
    onSuccess: async (_data, variables) => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.adminCurriculum.questions(variables.lessonId),
      });
    },
  });
}
