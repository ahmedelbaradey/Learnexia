/**
 * useUpdateSubject — PUT /api/learning/Subjects/Update (AdminOnly).
 *
 * Updates an existing subject. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.subject(id) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditSubjectDto } from './curriculum.types';

export function useUpdateSubject(): UseMutationResult<string, Error, EditSubjectDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, EditSubjectDto>({
    mutationFn: (body) =>
      client.put<string>('/api/learning/Subjects/Update', { body }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.subject(variables.id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
