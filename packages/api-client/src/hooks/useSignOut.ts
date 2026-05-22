/**
 * useSignOut — server-side sign-out mutation (authenticated).
 *
 * Wraps the NSwag `signOut` method to invalidate the server session. The caller
 * ALWAYS calls `authStore.signOut()` afterwards regardless of success/failure
 * (the local session must be clearable even when the network call fails) — see
 * Design Spec Screen 11. Public signature unchanged: `() => void` result.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';

export function useSignOut(): UseMutationResult<void, Error, void> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (): Promise<void> =>
      // SignOutCommand is an empty body; the typed method requires the arg.
      unwrapEnvelope(client.signOut({})).then(() => undefined),
  });
}
