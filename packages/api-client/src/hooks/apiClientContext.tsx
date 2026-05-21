/**
 * React context that supplies the configured `ApiClient` to hooks.
 *
 * The app shell creates ONE client (with platform token storage + sign-out
 * wiring) and provides it here. Hooks read it via `useApiClient()` so screens
 * never construct a client or call the API directly.
 */

import { createContext, useContext, type ReactNode } from 'react';

import type { ApiClient } from '../client/apiClient';

const ApiClientContext = createContext<ApiClient | null>(null);

export interface ApiClientProviderProps {
  client: ApiClient;
  children: ReactNode;
}

export function ApiClientProvider({ client, children }: ApiClientProviderProps) {
  return (
    <ApiClientContext.Provider value={client}>
      {children}
    </ApiClientContext.Provider>
  );
}

export function useApiClient(): ApiClient {
  const client = useContext(ApiClientContext);
  if (!client) {
    throw new Error(
      'useApiClient must be used within an <ApiClientProvider>. Provide a client created with createApiClient().',
    );
  }
  return client;
}
