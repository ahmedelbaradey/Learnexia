'use client';

/**
 * useSignOut (FE-5) — admin sign-out.
 *
 * Flow:
 *   1. POST /api/Users/Authentication/Sign-Out (best-effort / fire-and-forget —
 *      a network failure must NOT block local sign-out; the api-client's
 *      single-flight 401 refresh already handles an expired access token here).
 *   2. `authStore.signOut()` — clears in-memory tokens + sessionStorage and
 *      flips status to `'signed-out'`.
 *   3. Redirect to `/login`.
 *
 * Returns a stable async callback.
 */

import { useApiClient } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared/stores';
import { useRouter } from 'next/navigation';
import { useCallback } from 'react';

const SIGN_OUT_PATH = '/api/Users/Authentication/Sign-Out';

export function useSignOut(): () => Promise<void> {
  const client = useApiClient();
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
      await storeSignOut();
      router.replace('/login');
    }
  }, [client, storeSignOut, router]);
}
