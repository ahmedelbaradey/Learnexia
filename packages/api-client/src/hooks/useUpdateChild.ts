/**
 * useUpdateChild — PUT /api/Parent/Update-Child (authenticated).
 *
 * Wraps the NSwag `updateChild` method and unwraps to the typed
 * `UpdatedChildResponse`. The acting parent is resolved server-side from the
 * JWT — never sent in the body. On success it invalidates the myChildren query
 * so the grid refreshes with the latest name/grade/language. Drives the
 * Edit-Child sheet's save action (Design Spec My-Children grid).
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
