/**
 * useUpdateUnit — PUT /api/learning/Units/Update (AdminOnly).
 *
 * Updates a unit. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.units(subjectId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { EditUnitDto } from './curriculum.types';

export function useUpdateUnit(): UseMutationResult<string, Error, EditUnitDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, EditUnitDto>({
    mutationFn: (body) =>
      client.put<string>('/api/learning/Units/Update', { body }),
    onSuccess: async (_data, variables) => {
      await Promise.all([
        queryClient.invalidateQueries({
          queryKey: queryKeys.adminCurriculum.units(variables.subjectId),
        }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
