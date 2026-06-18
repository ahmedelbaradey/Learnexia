/**
 * useDeleteUnit — DELETE /api/learning/Units?id= (AdminOnly).
 *
 * Soft-deletes a unit. A non-empty unit (has lessons) returns Successed=false.
 * id is sent as a QUERY PARAM.
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.units(subjectId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface DeleteUnitInput {
  id: number;
  subjectId: number;
}

export function useDeleteUnit(): UseMutationResult<string, Error, DeleteUnitInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, DeleteUnitInput>({
    mutationFn: ({ id }) =>
      client.delete<string>(`/api/learning/Units`, { query: { id } }),
    onSuccess: async (_data, { subjectId }) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.units(subjectId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
