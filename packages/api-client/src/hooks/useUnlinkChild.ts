/**
 * useUnlinkChild — DELETE /api/Parent/Unlink-Child (authenticated).
 *
 * Wraps the NSwag `unlinkChild` method (HTTP DELETE with body `{ childId }`).
 * The BE enforces a last-parent guard — if the child has no other parent linked,
 * the endpoint returns 400 (BadRequest). The caller must handle this 400 case
 * via `ServerErrorBanner` with `byStatus: { 400: 'parent.settings.linkedChildren.unlinkLastParentError' }`.
 *
 * On success: invalidates `queryKeys.family.myChildren()`.
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
  boolean | null | undefined,
  Error,
  UnlinkChildCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: UnlinkChildCommand): Promise<boolean | null | undefined> =>
      unwrapEnvelope(client.unlinkChild(input)) as Promise<boolean | null | undefined>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.family.myChildren(),
      });
    },
  });
}
