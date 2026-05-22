'use client';

/**
 * Root client provider stack for the admin dashboard.
 *
 * Order (outer → inner):
 *   1. LearnexiaProvider  — wraps TamaguiProvider (dark theme) + locale/RTL
 *                           context. Admin ships English-first (Design Spec §7).
 *   2. QueryClientProvider — TanStack Query (all server data).
 *   3. ApiClientProvider   — supplies the configured ApiClient to hooks.
 *
 * AuthBoot (a child) performs the one-time startup wiring:
 *   - `authStore.setStorage(getTokenStorage())` so the store and the api-client
 *     read/write the SAME sessionStorage token source.
 *   - `authStore.hydrate()` to load any persisted tokens into memory and resolve
 *     the `'unknown'` → `'signed-in' | 'signed-out'` status (drives the guard).
 */

import { ApiClientProvider } from '@learnexia/api-client';
import { LearnexiaProvider } from '@learnexia/design-system';
import { useAuthStore } from '@learnexia/shared/stores';
import { QueryClientProvider, QueryClient } from '@tanstack/react-query';
import { createQueryClient } from '@learnexia/api-client';
import { useEffect, useRef, useState, type ReactNode } from 'react';

import { ADMIN_LOCALE } from '../lib/strings';
import { getApiClient, getTokenStorage } from '../lib/apiClient';

function AuthBoot({ children }: { children: ReactNode }) {
  const setStorage = useAuthStore((s) => s.setStorage);
  const hydrate = useAuthStore((s) => s.hydrate);
  const booted = useRef(false);

  useEffect(() => {
    if (booted.current) return;
    booted.current = true;
    // Share the SAME storage instance with the api-client so token reads/writes
    // are consistent across the interceptor and the store.
    setStorage(getTokenStorage());
    void hydrate();
  }, [setStorage, hydrate]);

  return <>{children}</>;
}

export function Providers({ children }: { children: ReactNode }) {
  // Stable per-mount QueryClient (avoids cache loss on re-render).
  const [queryClient] = useState<QueryClient>(() => createQueryClient());
  // ApiClient is a lazy singleton; resolved on the client only.
  const [apiClient] = useState(() => getApiClient());

  return (
    <LearnexiaProvider locale={ADMIN_LOCALE} theme="dark">
      <QueryClientProvider client={queryClient}>
        <ApiClientProvider client={apiClient}>
          <AuthBoot>{children}</AuthBoot>
        </ApiClientProvider>
      </QueryClientProvider>
    </LearnexiaProvider>
  );
}
