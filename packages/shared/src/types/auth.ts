/**
 * Auth domain types — mirror the Identity module contract verified in
 * `packages/api-client/swagger.json` (`JwtAuthResponse`, `RefreshToken`,
 * `SessionTimeoutInfo`, `SignInCommand`, `RefreshTokenCommand`).
 *
 * These are hand-written here (not generated) because they are the shared
 * vocabulary of the auth store + api-client refresh flow. api-client may also
 * import the generated equivalents; these stay in lock-step.
 */

import type { Role } from '../constants';

/** swagger: `RefreshToken` (the nested object inside JwtAuthResponse). */
export interface RefreshTokenInfo {
  userName?: string | null;
  /** ISO date-time string. */
  expireAt: string;
  tokenString?: string | null;
}

/** swagger: `SessionTimeoutInfo`. */
export interface SessionTimeoutInfo {
  timeoutMinutes: number;
  slidingExpiration: boolean;
  /** ISO date-time string. */
  expiresAt: string;
}

/** swagger: `JwtAuthResponse` — the auth payload returned by Sign-In / Refresh. */
export interface JwtAuthResponse {
  accessToken?: string | null;
  refreshToken?: RefreshTokenInfo | null;
  userId: number;
  isFirstLogin?: boolean | null;
  sessionTimeout?: SessionTimeoutInfo | null;
  sessionId?: string | null;
}

/** swagger: `SignInCommand` — body for POST /api/Users/Authentication/Sign-In. */
export interface SignInInput {
  userName: string;
  password: string;
}

/**
 * swagger: `RefreshTokenCommand` — body for
 * POST /api/Users/Authentication/Refresh-Token. The refresh token travels in
 * the request BODY (not a header), per the backend contract.
 */
export interface RefreshTokenInput {
  accessToken: string;
  refreshToken: string;
}

/**
 * The authenticated user as held in client state. This is UI/identity state
 * (id, display name, roles, locale) — NOT server data. Profile data fetched
 * from the API is owned by TanStack Query in api-client, not the auth store.
 */
export interface CurrentUser {
  id: number;
  userName?: string | null;
  fullName?: string | null;
  email?: string | null;
  roles: Role[];
  preferredLocale?: string | null;
}
