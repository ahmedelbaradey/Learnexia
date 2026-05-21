/**
 * ApiQueryProvider — wraps children in a TanStack Query `QueryClientProvider`.
 *
 * The app shell can either use this helper (it owns a default client) or pass
 * its own `client`. Framework-agnostic — works in both Expo and Next.
 */

import { QueryClientProvider, type QueryClient } from '@tanstack/react-query';
import { useState, type ReactNode } from 'react';

import { createQueryClient } from './queryClient';

export interface ApiQueryProviderProps {
  children: ReactNode;
  /** Optionally supply a shared client; one is created per-mount otherwise. */
  client?: QueryClient;
}

export function ApiQueryProvider({ children, client }: ApiQueryProviderProps) {
  // Stable client per mount when one is not injected (avoids re-creating on
  // every render, which would drop the cache).
  const [fallback] = useState(() => createQueryClient());
  const active = client ?? fallback;
  return <QueryClientProvider client={active}>{children}</QueryClientProvider>;
}
