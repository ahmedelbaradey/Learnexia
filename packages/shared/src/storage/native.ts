/**
 * Native (Expo) token storage backed by `expo-secure-store`.
 *
 * `expo-secure-store` stores values in the iOS Keychain / Android Keystore —
 * encrypted, app-scoped, and not readable by other apps. This is the secure
 * choice on device.
 *
 * `expo-secure-store` is an OPTIONAL peer: this package is framework-agnostic
 * and must type-check without Expo installed, so the module is imported
 * dynamically and typed structurally. The student-app (which has Expo) wires
 * this strategy; web/test consumers never load it.
 */

import type { AuthTokens, TokenStorage } from './tokenStorage';
import { TOKEN_STORAGE_KEYS } from './tokenStorage';

/** The slice of the `expo-secure-store` surface we use. */
export interface SecureStoreLike {
  getItemAsync(key: string): Promise<string | null>;
  setItemAsync(key: string, value: string): Promise<void>;
  deleteItemAsync(key: string): Promise<void>;
}

/**
 * Build a native token storage from an injected SecureStore module.
 *
 * The app passes its `expo-secure-store` import in (dependency injection keeps
 * this package free of an Expo dependency):
 *
 * ```ts
 * import * as SecureStore from 'expo-secure-store';
 * const storage = createNativeTokenStorage(SecureStore);
 * ```
 */
export function createNativeTokenStorage(secureStore: SecureStoreLike): TokenStorage {
  return {
    async getTokens(): Promise<AuthTokens | null> {
      const [accessToken, refreshToken] = await Promise.all([
        secureStore.getItemAsync(TOKEN_STORAGE_KEYS.accessToken),
        secureStore.getItemAsync(TOKEN_STORAGE_KEYS.refreshToken),
      ]);
      if (!accessToken || !refreshToken) return null;
      return { accessToken, refreshToken };
    },
    async setTokens(tokens: AuthTokens): Promise<void> {
      await Promise.all([
        secureStore.setItemAsync(TOKEN_STORAGE_KEYS.accessToken, tokens.accessToken),
        secureStore.setItemAsync(TOKEN_STORAGE_KEYS.refreshToken, tokens.refreshToken),
      ]);
    },
    async clear(): Promise<void> {
      await Promise.all([
        secureStore.deleteItemAsync(TOKEN_STORAGE_KEYS.accessToken),
        secureStore.deleteItemAsync(TOKEN_STORAGE_KEYS.refreshToken),
      ]);
    },
  };
}
