/**
 * useRegisterParent — parent self-registration mutation.
 *
 * Wraps the NSwag `registerParent` method and unwraps the envelope. Anonymous
 * (the transport skips auth for the Register-Parent path). Returns the typed
 * `JwtAuthResponse`; the caller persists tokens via `authStore.setTokens` and
 * routes to onboarding.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { JwtAuthResponse, RegisterParentCommand } from '../schemas';

export function useRegisterParent(): UseMutationResult<
  JwtAuthResponse,
  Error,
  RegisterParentCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: RegisterParentCommand): Promise<JwtAuthResponse> =>
      unwrapEnvelope(client.registerParent(input)) as Promise<JwtAuthResponse>,
  });
}
