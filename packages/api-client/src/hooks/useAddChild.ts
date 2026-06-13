/**
 * useAddChild — parent provisions a new child (authenticated).
 *
 * Wraps the NSwag `addChild` method and unwraps to the typed `AddedChildResponse`.
 * The acting parent is resolved server-side from the JWT — never sent in the
 * body. The Add-Child screen calls this once per child (sequential loop) with
 * per-child success/failure feedback (Design Spec Screen 5). On success,
 * `queryKeys.family.myChildren()` is invalidated automatically so the
 * My-Children list is always fresh regardless of the navigation path taken
 * after onboarding (fresh-mount path AND SPA navigation path).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { AddChildCommand, AddedChildResponse } from '../schemas';

export function useAddChild(): UseMutationResult<
  AddedChildResponse,
  Error,
  AddChildCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (input: AddChildCommand): Promise<AddedChildResponse> =>
      unwrapEnvelope(client.addChild(input)) as Promise<AddedChildResponse>,
    onSuccess: async () => {
      // Invalidate myChildren so the list refreshes everywhere.
      // Also invalidate auth.me so `hasChildren` is up to date — guards that
      // read hasChildren (e.g. useAuthRoute) see the updated state immediately
      // after the first successful add (AC-3.3 / OQ-1).
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.family.myChildren() }),
        queryClient.invalidateQueries({ queryKey: queryKeys.auth.me() }),
      ]);
    },
  });
}
