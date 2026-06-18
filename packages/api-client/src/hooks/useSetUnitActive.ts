/**
 * useSetUnitActive — PUT /api/learning/Units/{id}/Active (AdminOnly).
 *
 * Activates or deactivates a unit. No confirm dialog (reversible action).
 *
 * Body: { unitId: number, isActive: boolean }
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.units(subjectId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface SetUnitActiveInput {
  id: number;
  subjectId: number;
  isActive: boolean;
}

export function useSetUnitActive(): UseMutationResult<string, Error, SetUnitActiveInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, SetUnitActiveInput>({
    mutationFn: ({ id, isActive }) =>
      client.put<string>(`/api/learning/Units/${id}/Active`, {
        body: { unitId: id, isActive },
      }),
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
