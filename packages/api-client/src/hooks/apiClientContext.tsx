/**
 * React context that supplies the configured `ApiClient` to hooks.
 *
 * The app shell creates ONE client (with platform token storage + sign-out
 * wiring) and provides it here. Hooks read it via `useApiClient()` so screens
 * never construct a client or call the API directly.
 *
 * The same provider also derives the NSwag-generated typed client (wired to the
 * `ApiClient` transport) once per client instance, exposed via `useTypedClient()`.
 * Feature hooks call the typed client's per-endpoint methods and unwrap with
 * `unwrapEnvelope`.
 */

import { createContext, useContext, useMemo, type ReactNode } from 'react';

import type { ApiClient } from '../client/apiClient';
import { createTypedClient, type TypedApiClient } from '../client/typedClient';

interface ApiClientContextValue {
  client: ApiClient;
  typed: TypedApiClient;
}

const ApiClientContext = createContext<ApiClientContextValue | null>(null);

export interface ApiClientProviderProps {
  client: ApiClient;
  children: ReactNode;
}

export function ApiClientProvider({ client, children }: ApiClientProviderProps) {
  const value = useMemo<ApiClientContextValue>(
    () => ({ client, typed: createTypedClient(client) }),
    [client],
  );
  return (
    <ApiClientContext.Provider value={value}>
      {children}
    </ApiClientContext.Provider>
  );
}

function useApiClientContext(): ApiClientContextValue {
  const value = useContext(ApiClientContext);
  if (!value) {
    throw new Error(
      'useApiClient must be used within an <ApiClientProvider>. Provide a client created with createApiClient().',
    );
  }
  return value;
}

/** The hand-written transport client (auth + 401-refresh + envelope unwrap). */
export function useApiClient(): ApiClient {
  return useApiClientContext().client;
}

/** The NSwag-generated typed client (one method per endpoint), wired to transport. */
export function useTypedClient(): TypedApiClient {
  return useApiClientContext().typed;
}
