/**
 * useGoogleSignIn — POST /api/Users/Authentication/Google-SignIn (anonymous).
 *
 * Wraps the NSwag `googleSignIn(GoogleSignInCommand)` method and returns the
 * typed `JwtAuthResponse`. The transport recognizes the Google-SignIn path as
 * anonymous (added to ANONYMOUS_PATH_SUFFIXES in apiClient.ts — FE-0a) so it
 * never attaches a bearer token.
 *
 * Token persistence is the CALLER's responsibility (mirrors `useSignIn`). The
 * caller should call `authStore.setTokens(...)` and `router.replace('/')` on
 * success, never storing tokens inside this hook.
 *
 * Security: `idToken` is sent only to the backend and is NEVER logged. The
 * backend links or creates the parent account by Google email; the FE sends
 * only `{ idToken }` and the BE is authoritative for linking logic.
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
