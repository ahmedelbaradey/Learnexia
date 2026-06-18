/**
 * useReorderUnits — PUT /api/learning/Units/Reorder (AdminOnly).
 *
 * Persists new unit order within a single subject. Array position = new sequenceOrder.
 * Body: { unitIds: number[] } — max 200 per request.
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.units(subjectId) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface ReorderUnitsInput {
  subjectId: number;
  unitIds: number[];
}

export function useReorderUnits(): UseMutationResult<string, Error, ReorderUnitsInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, ReorderUnitsInput>({
    mutationFn: ({ unitIds }) =>
      client.put<string>('/api/learning/Units/Reorder', { body: { unitIds } }),
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
