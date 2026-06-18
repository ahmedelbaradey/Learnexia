/**
 * useDeleteSubject — DELETE /api/learning/Subjects?id= (AdminOnly).
 *
 * Soft-deletes a subject (sets IsDeleted=true). A non-empty subject
 * (has units) returns Successed=false — surface that error; do NOT remove row.
 *
 * id is sent as a QUERY PARAM, not a route param or body.
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteSubjectInput {
  id: number;
}

export function useDeleteSubject(): UseMutationResult<string, Error, DeleteSubjectInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteSubjectInput>({
    mutationFn: ({ id }) =>
      client.delete<string>(`/api/learning/Subjects`, { query: { id } }),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all });
    },
  });
}
