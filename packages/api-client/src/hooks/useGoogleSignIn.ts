/**
 * useGoogleSignIn — POST /api/Users/Authentication/Google-SignIn (anonymous).
 *
 * Wraps the NSwag `googleSignIn` method. Takes the Google ID token (a JWT
 * string obtained from Google Identity Services on web) and unwraps to the
 * typed `JwtAuthResponse`. The caller is responsible for persisting the tokens
 * via `authStore.setTokens` (same pattern as `useSignIn`). This hook is a pure
 * network mutation; token persistence and navigation are the caller's concern.
 *
 * Dev no-op: gated on EXPO_PUBLIC_GOOGLE_CLIENT_ID being set. If unset the
 * button is disabled and this hook is never called. The hook itself does not
 * check the env — the gating lives in the UI layer.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { GoogleSignInCommand, JwtAuthResponse } from '../schemas';

export function useGoogleSignIn(): UseMutationResult<
  JwtAuthResponse,
  Error,
  GoogleSignInCommand
> {
  const client = useTypedClient();
  return useMutation({
    mutationFn: (input: GoogleSignInCommand): Promise<JwtAuthResponse> =>
      unwrapEnvelope(client.googleSignIn(input)) as Promise<JwtAuthResponse>,
  });
}
