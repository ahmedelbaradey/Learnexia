/**
 * The admin app's single configured `ApiClient` instance.
 *
 * Wires the framework-agnostic `@learnexia/api-client` to the web platform:
 *  - `baseUrl`        ← NEXT_PUBLIC_API_URL (build/runtime env)
 *  - `tokenStorage`   ← `createWebTokenStorage()` (sessionStorage; same storage
 *                       the authStore uses, so reads/writes stay consistent)
 *  - `onSignOut`      ← `authStore.signOut()` (fires when a 401 refresh fails)
 *  - `onTokensRefreshed` ← `authStore.setTokens()` (keeps in-memory tokens in
 *                       sync after a silent refresh)
 *
 * Created lazily once on the client; the same `tokenStorage` instance is shared
 * with the authStore so a single sessionStorage source of truth is used.
 */

import { createApiClient, type ApiClient } from '@learnexia/api-client';
import { useAuthStore } from '@learnexia/shared/stores';
import { createWebTokenStorage, type TokenStorage } from '@learnexia/shared/storage';

let storageSingleton: TokenStorage | null = null;
let clientSingleton: ApiClient | null = null;

/** The shared sessionStorage-backed token storage (one instance per app). */
export function getTokenStorage(): TokenStorage {
  if (!storageSingleton) {
    storageSingleton = createWebTokenStorage();
  }
  return storageSingleton;
}

/** Resolve the API base URL from env; throws early if misconfigured. */
function resolveBaseUrl(): string {
  const url = process.env.NEXT_PUBLIC_API_URL;
  if (!url) {
    throw new Error(
      'NEXT_PUBLIC_API_URL is not set. Copy .env.local.example to .env.local and set the API base URL.',
    );
  }
  return url;
}

/** The configured ApiClient singleton (created on first use, client-side). */
export function getApiClient(): ApiClient {
  if (!clientSingleton) {
    const tokenStorage = getTokenStorage();
    clientSingleton = createApiClient({
      baseUrl: resolveBaseUrl(),
      tokenStorage,
      onSignOut: () => {
        void useAuthStore.getState().signOut();
      },
      onTokensRefreshed: (tokens) => {
        void useAuthStore.getState().setTokens(tokens);
      },
    });
  }
  return clientSingleton;
}
