/**
 * Admin-local JWT helpers — decode the access-token payload to read role
 * claims for the admin gate.
 *
 * WHY decode the JWT (and NOT call GetUserProfile): per the lead decision for
 * P1-10, roles ARE present in the issued access token's claims. The admin gate
 * reads them directly from the token rather than making a second
 * `GetUserProfile` round-trip (which is an AdminOnly-adjacent endpoint and adds
 * latency + a failure surface). This decode is for GATING/UX only — it is NOT a
 * security boundary. The backend `AdminOnly` policy is the authoritative gate
 * for any admin API call.
 *
 * SECURITY: this only base64url-decodes the payload segment; it does NOT verify
 * the signature (impossible client-side without the secret, and unnecessary —
 * the server validates every request). No token value is logged.
 */

import { ROLES, type Role } from '@learnexia/shared/constants';

/** .NET role claim type URIs / short names that may carry roles in the JWT. */
const ROLE_CLAIM_KEYS = [
  'role',
  'roles',
  'http://schemas.microsoft.com/ws/2008/06/identity/claims/role',
];

/** Base64url → UTF-8 string. Works in browser and SSR (Buffer fallback). */
function base64UrlDecode(segment: string): string | null {
  try {
    const padded = segment.replace(/-/g, '+').replace(/_/g, '/');
    const pad = padded.length % 4 === 0 ? '' : '='.repeat(4 - (padded.length % 4));
    const b64 = padded + pad;
    if (typeof atob === 'function') {
      const binary = atob(b64);
      // Decode UTF-8 bytes (handles non-ASCII names safely).
      const bytes = Uint8Array.from(binary, (c) => c.charCodeAt(0));
      return new TextDecoder().decode(bytes);
    }
    // SSR fallback.
    return Buffer.from(b64, 'base64').toString('utf-8');
  } catch {
    return null;
  }
}

/** Decode a JWT payload (claims) without verifying the signature. */
export function decodeJwtPayload(token: string): Record<string, unknown> | null {
  const parts = token.split('.');
  if (parts.length < 2 || !parts[1]) return null;
  const json = base64UrlDecode(parts[1]);
  if (!json) return null;
  try {
    const parsed = JSON.parse(json);
    return parsed && typeof parsed === 'object' ? (parsed as Record<string, unknown>) : null;
  } catch {
    return null;
  }
}

/** Pull every role string out of the decoded claims (any of the known keys). */
export function extractRolesFromClaims(claims: Record<string, unknown>): string[] {
  const out: string[] = [];
  for (const key of ROLE_CLAIM_KEYS) {
    const value = claims[key];
    if (typeof value === 'string') {
      out.push(value);
    } else if (Array.isArray(value)) {
      for (const v of value) {
        if (typeof v === 'string') out.push(v);
      }
    }
  }
  return out;
}

/** Decode the token and return its role claims (empty array if none/invalid). */
export function getRolesFromToken(token: string | null | undefined): string[] {
  if (!token) return [];
  const claims = decodeJwtPayload(token);
  if (!claims) return [];
  return extractRolesFromClaims(claims);
}

/**
 * Case-insensitive admin check. JWT roles are PascalCase (`Admin`,
 * `SuperAdmin`); shared `ROLES` constants are lowercase. Compare lower-vs-lower.
 */
export function isAdminRoleList(roles: readonly string[]): boolean {
  const lowered = roles.map((r) => r.toLowerCase());
  return lowered.includes(ROLES.Admin) || lowered.includes(ROLES.SuperAdmin);
}

/** Convenience: does this access token grant admin/superadmin? */
export function tokenGrantsAdmin(token: string | null | undefined): boolean {
  return isAdminRoleList(getRolesFromToken(token));
}

/**
 * Normalize raw JWT role strings to the shared lowercase `Role` union, keeping
 * only the roles the app knows about (admin/superadmin/parent/student).
 */
export function normalizeRoles(roles: readonly string[]): Role[] {
  const known = new Set<string>(Object.values(ROLES));
  const out: Role[] = [];
  for (const r of roles) {
    const lower = r.toLowerCase();
    if (known.has(lower) && !out.includes(lower as Role)) {
      out.push(lower as Role);
    }
  }
  return out;
}
