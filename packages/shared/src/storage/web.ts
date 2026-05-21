/**
 * Web token storage.
 *
 * SECURITY POSTURE (documented for the security pass):
 * The most secure web option is an HttpOnly + Secure + SameSite cookie set by
 * the backend, so JS can never read the token (mitigates XSS token theft).
 * That requires a backend cookie-auth path which is NOT in this task's scope.
 *
 * Until that exists, this package's default web strategy uses `sessionStorage`
 * (NOT `localStorage`):
 *   - `sessionStorage` is cleared when the tab closes -> smaller exposure window
 *     than `localStorage` (which persists indefinitely across sessions).
 *   - It is per-origin and per-tab.
 * `localStorage` is intentionally avoided: it survives indefinitely and is the
 * classic XSS exfiltration target.
 *
 * The cookie-backed strategy is provided as `createCookieTokenStorageProxy`
 * for when the backend exposes cookie auth: in that mode the browser holds the
 * (HttpOnly) cookie and this strategy is a no-op token holder, since JS must
 * not — and cannot — read HttpOnly cookies.
 */

import type { AuthTokens, TokenStorage } from './tokenStorage';
import { TOKEN_STORAGE_KEYS } from './tokenStorage';

interface WebStorageLike {
  getItem(key: string): string | null;
  setItem(key: string, value: string): void;
  removeItem(key: string): void;
}

function resolveSessionStorage(): WebStorageLike | null {
  if (typeof globalThis === 'undefined') return null;
  const candidate = (globalThis as { sessionStorage?: WebStorageLike }).sessionStorage;
  return candidate ?? null;
}

/**
 * `sessionStorage`-backed web token storage (default web strategy).
 * Falls back to a non-persistent no-op holder when no storage is available
 * (e.g. SSR), so callers never crash.
 */
export function createWebTokenStorage(): TokenStorage {
  const store = resolveSessionStorage();

  if (!store) {
    // SSR / no DOM: degrade to in-memory-ish (nothing persisted).
    let mem: AuthTokens | null = null;
    return {
      async getTokens() {
        return mem;
      },
      async setTokens(tokens) {
        mem = tokens;
      },
      async clear() {
        mem = null;
      },
    };
  }

  return {
    async getTokens(): Promise<AuthTokens | null> {
      const accessToken = store.getItem(TOKEN_STORAGE_KEYS.accessToken);
      const refreshToken = store.getItem(TOKEN_STORAGE_KEYS.refreshToken);
      if (!accessToken || !refreshToken) return null;
      return { accessToken, refreshToken };
    },
    async setTokens(tokens: AuthTokens): Promise<void> {
      store.setItem(TOKEN_STORAGE_KEYS.accessToken, tokens.accessToken);
      store.setItem(TOKEN_STORAGE_KEYS.refreshToken, tokens.refreshToken);
    },
    async clear(): Promise<void> {
      store.removeItem(TOKEN_STORAGE_KEYS.accessToken);
      store.removeItem(TOKEN_STORAGE_KEYS.refreshToken);
    },
  };
}

/**
 * Cookie-auth proxy for the future HttpOnly-cookie backend mode.
 *
 * When the backend manages an HttpOnly auth cookie, JS cannot (and must not)
 * read the token. This storage therefore reports "no readable tokens" while
 * still satisfying the interface, so the api-client interceptor simply relies
 * on the browser attaching the cookie to credentialed requests.
 */
export function createCookieTokenStorageProxy(): TokenStorage {
  return {
    async getTokens() {
      // HttpOnly cookie is not readable by JS by design.
      return null;
    },
    async setTokens() {
      // No-op: the backend Set-Cookie owns this.
    },
    async clear() {
      // No-op: clearing is the backend's responsibility (logout endpoint).
    },
  };
}
