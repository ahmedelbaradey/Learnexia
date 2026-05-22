'use client';

/**
 * useAdminGuard (FE-4) — client-side admin route guard.
 *
 * Maps `authStore` state → one of three guard states (Design Spec §5a):
 *   - 'loading'   : status === 'unknown' (hydration in progress). The caller
 *                   renders the full-page skeleton — NO protected content and
 *                   NO redirect yet, so nothing flashes.
 *   - 'redirecting': signed-out OR signed-in-but-not-admin. The hook issues a
 *                    `router.replace('/login')` and the caller renders nothing.
 *   - 'authorized': signed-in AND the access-token role claims include
 *                   Admin/SuperAdmin (case-insensitive).
 *
 * The admin role is read by DECODING the access token's role claims (see
 * `lib/jwt.ts`) — no extra profile round-trip. This guard is a UX boundary
 * only; the backend `AdminOnly` policy is the real authorization gate. With
 * sessionStorage tokens the Next.js edge cannot enforce this server-side, so
 * the guard is client-only (documented in `middleware.ts`).
 */

import { useAuthStore } from '@learnexia/shared/stores';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

import { tokenGrantsAdmin } from '../jwt';

export type AdminGuardState = 'loading' | 'redirecting' | 'authorized';

export function useAdminGuard(): AdminGuardState {
  const status = useAuthStore((s) => s.status);
  const accessToken = useAuthStore((s) => s.accessToken);
  const router = useRouter();

  const isAdmin = tokenGrantsAdmin(accessToken);
  const state: AdminGuardState =
    status === 'unknown'
      ? 'loading'
      : status === 'signed-in' && isAdmin
        ? 'authorized'
        : 'redirecting';

  useEffect(() => {
    if (state === 'redirecting') {
      router.replace('/login');
    }
  }, [state, router]);

  return state;
}
