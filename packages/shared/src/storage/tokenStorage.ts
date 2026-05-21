/**
 * Token-storage abstraction.
 *
 * One framework-agnostic interface (`TokenStorage`) that hides where tokens
 * actually live. Platform strategies sit behind it:
 *   - native (Expo): `expo-secure-store` (Keychain / Keystore-backed)
 *   - web: a secure-by-default strategy (see `web.ts`)
 *
 * SECURITY: the api-client refresh interceptor reads/writes ONLY through this
 * interface, so it never depends on a concrete platform and tokens never have
 * to be passed around as plain globals. Implementations must NEVER log token
 * values.
 */

export interface AuthTokens {
  accessToken: string;
  refreshToken: string;
}

/**
 * Storage contract for the auth tokens. All methods are async so the same
 * interface fits async-only backends (SecureStore, IndexedDB) and sync ones
 * (sessionStorage, wrapped in a resolved promise).
 */
export interface TokenStorage {
  getTokens(): Promise<AuthTokens | null>;
  setTokens(tokens: AuthTokens): Promise<void>;
  clear(): Promise<void>;
}

/** Storage keys used across implementations (kept identical for portability). */
export const TOKEN_STORAGE_KEYS = {
  accessToken: 'lx.auth.accessToken',
  refreshToken: 'lx.auth.refreshToken',
} as const;

/**
 * In-memory fallback. Safe everywhere, but tokens do NOT survive a reload —
 * use a real platform strategy in apps. Useful for tests and SSR.
 */
export function createInMemoryTokenStorage(): TokenStorage {
  let tokens: AuthTokens | null = null;
  return {
    async getTokens() {
      return tokens;
    },
    async setTokens(next) {
      tokens = next;
    },
    async clear() {
      tokens = null;
    },
  };
}
