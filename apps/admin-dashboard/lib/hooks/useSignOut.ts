'use client';

/**
 * useSignOut (FE-5) — admin sign-out.
 *
 * Flow:
 *   1. POST /api/Users/Authentication/Sign-Out (best-effort / fire-and-forget —
 *      a network failure must NOT block local sign-out; the api-client's
 *      single-flight 401 refresh already handles an expired access token here).
 *   2. `queryClient.clear()` — evicts all cached data (user/child PII) from the
 *      TanStack Query in-memory cache so it does not linger after sign-out.
 *      gcTime defaults to 5 min; without this call, PII would persist in memory
 *      until garbage collected even after the tokens are cleared.
 *   3. `authStore.signOut()` — clears in-memory tokens + sessionStorage and
 *      flips status to `'signed-out'`.
 *   4. Redirect to `/login`.
 *
 * Returns a stable async callback.
 */

import { useQueryClient } from '@tanstack/react-query';
import { useApiClient } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared/stores';
import { useRouter } from 'next/navigation';
import { useCallback } from 'react';

const SIGN_OUT_PATH = '/api/Users/Authentication/Sign-Out';

export function useSignOut(): () => Promise<void> {
  const client = useApiClient();
  const queryClient = useQueryClient();
  const storeSignOut = useAuthStore((s) => s.signOut);
  const router = useRouter();

  return useCallback(async () => {
    try {
      // Authenticated call; the client attaches the bearer token. Best-effort:
      // we still sign out locally even if this rejects (offline, expired, etc.).
      await client.post(SIGN_OUT_PATH);
    } catch {
      // Intentionally swallowed — never block local sign-out on a network error.
    } finally {
      // Clear all cached PII (user profiles, child data) from the in-memory
      // TanStack Query cache before redirecting.
      queryClient.clear();
      await storeSignOut();
      router.replace('/login');
    }
  }, [client, queryClient, storeSignOut, router]);
}
