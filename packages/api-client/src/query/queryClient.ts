/**
 * QueryClient factory + provider helper.
 *
 * Framework-agnostic: no Expo/Next imports. The app shell renders
 * `ApiQueryProvider` (or its own `QueryClientProvider`) near the root.
 *
 * Retry policy is auth-aware: never retry on `ApiAuthError` (a 401 the client
 * could not refresh) or `ValidationError` (422) — retrying those is pointless
 * and would loop the user. Other transient failures retry up to twice.
 */

import { QueryClient } from '@tanstack/react-query';

import { ApiAuthError, ValidationError } from '../client/errors';

export function createQueryClient(): QueryClient {
  return new QueryClient({
    defaultOptions: {
      queries: {
        staleTime: 30_000,
        gcTime: 5 * 60_000,
        retry: (failureCount, error) => {
          if (error instanceof ApiAuthError) return false;
          if (error instanceof ValidationError) return false;
          return failureCount < 2;
        },
        refetchOnWindowFocus: false,
      },
      mutations: {
        retry: false,
      },
    },
  });
}
