/**
 * useLinkChild — link an existing child account by email (authenticated).
 *
 * Wraps the NSwag `linkChild` method and unwraps to the typed
 * `LinkedChildResponse`. The acting parent is resolved server-side from the JWT.
 * On success, callers should invalidate the My-Children query (Design Spec
 * Screen 9).
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { LinkChildCommand, LinkedChildResponse } from '../schemas';

export function useLinkChild(): UseMutationResult<
  LinkedChildResponse,
  Error,
  LinkChildCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: LinkChildCommand): Promise<LinkedChildResponse> =>
      unwrapEnvelope(client.linkChild(input)) as Promise<LinkedChildResponse>,
  });
}
