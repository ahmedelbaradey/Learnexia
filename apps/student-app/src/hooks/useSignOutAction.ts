/**
 * useSignOutAction — sign-out that ALWAYS clears the local session.
 *
 * Calls the server Sign-Out endpoint (best-effort) then `authStore.signOut()`
 * regardless of the API result — local sign-out must never be blocked by a
 * network failure (Design Spec Screen 11 / Gap 7). The routing guard then
 * redirects to login.
 */
import { useSignOut } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared';
import { useRouter } from 'expo-router';
import { useCallback } from 'react';

export function useSignOutAction(): { signOut: () => Promise<void>; isPending: boolean } {
  const router = useRouter();
  const signOutMutation = useSignOut();
  const storeSignOut = useAuthStore((s) => s.signOut);

  const signOut = useCallback(async () => {
    try {
      await signOutMutation.mutateAsync();
    } catch {
      // Ignore — the local session must still be cleared.
    }
    await storeSignOut();
    router.replace('/(auth)/login');
  }, [signOutMutation, storeSignOut, router]);

  return { signOut, isPending: signOutMutation.isPending };
}
