/**
 * Client configuration + dependency injection seams.
 *
 * The client is framework-agnostic: it imports NO Expo/Next code. The host app
 * injects how tokens are read/written (`TokenStorage` from @learnexia/shared)
 * and what to do when the session is unrecoverable (`onSignOut`). This keeps
 * the package universal across web and native.
 */

import type { AuthTokens, TokenStorage } from '@learnexia/shared';

export const REFRESH_TOKEN_PATH = '/api/Users/Authentication/Refresh-Token';

/** Body for POST /api/Users/Authentication/Refresh-Token (refresh in BODY). */
export interface RefreshRequestBody {
  accessToken: string;
  refreshToken: string;
}

export interface ApiClientConfig {
  /** Base URL of the .NET API, e.g. `https://api.learnexia.app`. No trailing slash required. */
  baseUrl: string;

  /**
   * Token storage abstraction (from @learnexia/shared). The client reads the
   * access token to attach `Authorization`, and reads/writes both tokens during
   * refresh. Inject `ExpoSecureStore`/`Web` impls per platform.
   */
  tokenStorage: TokenStorage;

  /**
   * Called when the session is unrecoverable (refresh failed / no tokens on a
   * 401). Tokens have already been cleared from storage before this fires.
   * The app wires this to `authStore.signOut()` + navigation to sign-in.
   */
  onSignOut?: () => void | Promise<void>;

  /**
   * Optional hook to persist refreshed tokens into the in-memory auth store as
   * well as storage. The app wires this to `authStore.setTokens`. If omitted,
   * tokens are written through `tokenStorage` only.
   */
  onTokensRefreshed?: (tokens: AuthTokens) => void | Promise<void>;

  /** Default request headers merged into every request (e.g. Accept-Language). */
  defaultHeaders?: Record<string, string>;

  /** Override the fetch implementation (tests / non-global-fetch runtimes). */
  fetchImpl?: typeof fetch;
}
