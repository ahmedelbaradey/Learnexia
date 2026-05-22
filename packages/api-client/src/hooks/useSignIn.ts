/**
 * useSignIn — mutation example (the pattern other mutations follow).
 *
 * Wraps the NSwag-generated `signIn` method (typed `Client.signIn`) and unwraps
 * the `BaseResponse` envelope via `unwrapEnvelope`, returning the typed
 * `JwtAuthResponse`. Sign-in is anonymous: the transport recognizes the
 * Sign-In path and attaches no Authorization header (and skips 401-refresh).
 *
 * PUBLIC SIGNATURE IS STABLE — `useSignIn(): UseMutationResult<JwtAuthResponse,
 * Error, SignInCommand>`. P1-10 (admin sign-in) consumes this; do not change it.
 * The caller persists tokens via `authStore.setTokens`.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { JwtAuthResponse, SignInCommand } from '../schemas';

export function useSignIn(): UseMutationResult<
  JwtAuthResponse,
  Error,
  SignInCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: SignInCommand): Promise<JwtAuthResponse> =>
      unwrapEnvelope(client.signIn(input)) as Promise<JwtAuthResponse>,
  });
}
