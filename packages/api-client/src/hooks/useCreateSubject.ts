/**
 * useCreateSubject — POST /api/learning/Subjects/Create (AdminOnly).
 *
 * Creates a new subject. Returns BaseResponse<string> (a success message).
 * Caller must refetch via invalidation — do NOT read new state from the result.
 *
 * Invalidates: adminCurriculum.all (busts all subject lists + coverage).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddSubjectDto } from './curriculum.types';

export function useCreateSubject(): UseMutationResult<string, Error, AddSubjectDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddSubjectDto>({
    mutationFn: (body) =>
      client.post<string>('/api/learning/Subjects/Create', { body }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all });
    },
  });
}
