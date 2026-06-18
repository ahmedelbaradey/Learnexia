/**
 * useCreateUnit — POST /api/learning/Units/Create (AdminOnly).
 *
 * Creates a unit under a subject. Returns BaseResponse<string>.
 * Invalidates: adminCurriculum.units(subjectId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddUnitDto } from './curriculum.types';

export function useCreateUnit(): UseMutationResult<string, Error, AddUnitDto> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddUnitDto>({
    mutationFn: (body) =>
      client.post<string>('/api/learning/Units/Create', { body }),
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
