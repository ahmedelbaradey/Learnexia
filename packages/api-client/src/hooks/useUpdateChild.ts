/**
 * useUpdateChild — PUT /api/Parent/Update-Child (authenticated).
 *
 * Wraps the NSwag `updateChild` method. The caller provides a full
 * `UpdateChildCommand` (`{ childId, fullName, grade, language, country }`).
 * Grade must be 1..6 (product decision — 4 subjects, no Social Studies;
 * grade-transition preserves history).
 *
 * On success: invalidates `queryKeys.family.myChildren()` so the Linked-children
 * list refreshes with the new name.
 */
import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UpdateChildCommand, UpdatedChildResponse } from '../schemas';

export function useUpdateChild(): UseMutationResult<
  UpdatedChildResponse,
  Error,
  UpdateChildCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UpdateChildCommand): Promise<UpdatedChildResponse> =>
      unwrapEnvelope(client.updateChild(input)) as Promise<UpdatedChildResponse>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.family.myChildren(),
      });
    },
  });
}
