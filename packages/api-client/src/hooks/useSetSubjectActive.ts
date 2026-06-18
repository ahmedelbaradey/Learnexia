/**
 * useSetSubjectActive — PUT /api/learning/Subjects/{id}/Active (AdminOnly).
 *
 * Activates or deactivates a subject. No confirm dialog (reversible action).
 *
 * Body: { subjectId: number, isActive: boolean }
 * Route also carries the id: /{id}/Active (backend overwrites body id from route).
 * Returns: BaseResponse<string>.
 * Invalidates: adminCurriculum.subject(id) + adminCurriculum.all.
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface SetSubjectActiveInput {
  id: number;
  isActive: boolean;
}

export function useSetSubjectActive(): UseMutationResult<string, Error, SetSubjectActiveInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, SetSubjectActiveInput>({
    mutationFn: ({ id, isActive }) =>
      client.put<string>(`/api/learning/Subjects/${id}/Active`, {
        body: { subjectId: id, isActive },
      }),
    onSuccess: async (_data, { id }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.subject(id) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.all }),
      ]);
    },
  });
}
