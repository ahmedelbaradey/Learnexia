/**
 * useUnlinkChild — POST /api/Parent/Unlink-Child (authenticated).
 *
 * Wraps the NSwag `unlinkChild` method and unwraps to `boolean` (the server
 * returns a `BooleanBaseResponse`). The acting parent is resolved server-side
 * from the JWT. On success it invalidates the myChildren query so the grid
 * removes the now-unlinked child. Intended for a future "Unlink" affordance in
 * the Linked-Children settings tab (P2-12).
 */

import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UnlinkChildCommand } from '../schemas';

export function useUnlinkChild(): UseMutationResult<
  boolean,
  Error,
  UnlinkChildCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UnlinkChildCommand): Promise<boolean> =>
      unwrapEnvelope(client.unlinkChild(input)) as Promise<boolean>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.family.myChildren(),
      });
    },
  });
}
