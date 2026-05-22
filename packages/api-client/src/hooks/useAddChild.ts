/**
 * useAddChild — parent provisions a new child (authenticated).
 *
 * Wraps the NSwag `addChild` method and unwraps to the typed `AddedChildResponse`.
 * The acting parent is resolved server-side from the JWT — never sent in the
 * body. The Add-Child screen calls this once per child (sequential loop) with
 * per-child success/failure feedback (Design Spec Screen 5). On success, callers
 * should invalidate the My-Children query.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { AddChildCommand, AddedChildResponse } from '../schemas';

export function useAddChild(): UseMutationResult<
  AddedChildResponse,
  Error,
  AddChildCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: AddChildCommand): Promise<AddedChildResponse> =>
      unwrapEnvelope(client.addChild(input)) as Promise<AddedChildResponse>,
  });
}
