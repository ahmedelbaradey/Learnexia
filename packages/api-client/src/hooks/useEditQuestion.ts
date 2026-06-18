/**
 * useEditQuestion — PUT /api/Learning/Questions
 *
 * Updates a question's type/text/options/correctAnswer/difficulty.
 * Returns BaseResponse<string> (message). Auth: AdminOnly.
 *
 * Invalidates: adminCurriculum.questions(lessonId) + question(id).
 * No optimistic update — invalidate-and-refetch (Wave-1 decision D8).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditQuestionPayload } from './curriculum.types';

export interface EditQuestionInput extends EditQuestionPayload {
  /** Used for cache invalidation — not sent to the backend. */
  lessonId: number;
}

export function useEditQuestion(): UseMutationResult<string, Error, EditQuestionInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, EditQuestionInput>({
    mutationFn: ({ lessonId: _lessonId, ...payload }) =>
      client.put<string>('/api/Learning/Questions', { body: payload }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.questions(variables.lessonId),
        }),
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.question(variables.id),
        }),
      ]);
    },
  });
}
