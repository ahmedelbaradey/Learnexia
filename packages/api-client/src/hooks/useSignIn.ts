/**
 * useSignIn — mutation example.
 *
 * Pattern other mutations follow: take the client from context, call an
 * endpoint, let the client unwrap the `BaseResponse` envelope (so the hook gets
 * `data` directly or a typed error), expose typed `data`/`error` to callers.
 *
 * Sign-in is `skipAuth` (no Authorization header) and returns the unwrapped
 * `JwtAuthResponse`. The caller is responsible for persisting tokens via
 * `authStore.setTokens` + the token-storage abstraction.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import type { JwtAuthResponse, SignInCommand } from '../schemas';

export function useSignIn(): UseMutationResult<
  JwtAuthResponse,
  Error,
  SignInCommand
> {
  const client = useApiClient();
  return useMutation({
    mutationFn: (input: SignInCommand) =>
      client.post<JwtAuthResponse>('/api/Users/Authentication/Sign-In', {
        body: input,
        skipAuth: true,
      }),
  });
}
