/**
 * useAdminUserProfile — GET /api/Admin/Users/{id} (admin-only).
 *
 * Read-only admin-inspect profile for a single user. Includes both language
 * axes (`preferredLanguage` / `learningLanguage`) and child-only fields
 * (grade, nationality) that are absent from the list endpoint.
 *
 * Guards: query is disabled when `id <= 0` (backend returns 400 for id ≤ 0;
 * the FE treats both 400-on-detail and 404 as "not found" per brief Q4).
 *
 * Backend contract (P7-06):
 *   GET /api/Admin/Users/{id}
 *   Returns: BaseResponse<AdminUserProfileDto>
 *   id ≤ 0 → 400; not found → 404.
 *   Emits AdminActionPerformedEvent(UserViewed) post-read (best-effort audit).
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 *
 * `lastSignInAtUtc` is ALWAYS null — the backend has no LastSignInAt column.
 * Callers should label it "Sign-in activity: not tracked" (decision D5).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { AccountStatusValue } from '@learnexia/shared';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/Admin/AdminUserProfileDto.cs) ─

/**
 * Admin read-only profile DTO. Distinct from the self-service /Me profile so
 * admin-inspect fields are not leaked to the public profile endpoint.
 *
 * Language axes are SEPARATE by design — never merge or conflate them:
 *   `preferredLanguage` — UI/UX culture code, e.g. "ar-EG" or "en-US".
 *   `learningLanguage`  — curriculum medium for Math/Science ("ar" | "en").
 */
export interface AdminUserProfileDto {
  id: number;
  fullName: string;
  email: string;
  /** Primary role name. Null if no role assigned. */
  role: string | null;
  /** All roles assigned to the user. */
  roles: string[];
  /** Governance lifecycle status (0 Active, 1 Suspended, 2 Deleted). */
  accountStatus: AccountStatusValue;
  /** Whether the account can currently sign in. */
  isActive: boolean;
  /** Reason for the last status change. Null when status is Active. */
  lastStatusReason: string | null;
  /** UTC timestamp of the last status change. Null when never changed from Active. */
  statusChangedAtUtc: string | null;
  /** UTC creation timestamp. */
  createdAt: string;
  /**
   * Always null — no LastSignInAt column exists in the Identity store.
   * Label in UI as "Sign-in activity: not tracked".
   */
  lastSignInAtUtc: null;

  // ── Language axes (DISTINCT — do not merge) ───────────────────────────────
  /** UI/UX language preference. Culture code, e.g. "ar-EG" or "en-US". */
  preferredLanguage: string;
  /**
   * Medium-of-instruction language for the child's curriculum ("ar" | "en").
   * Present for all user records (defaults to "ar" for non-children).
   */
  learningLanguage: string;

  // ── Child-only fields (null for parents/admins) ───────────────────────────
  /** School grade 1–6. Non-null only for Student role. */
  grade: number | null;
  /** Country/nationality. Non-null only for Student role. */
  nationality: string | null;
  avatarUrl: string | null;
}

// ── Hook ───────────────────────────────────────────────────────────────────────

export function useAdminUserProfile(
  id: number,
): UseQueryResult<AdminUserProfileDto, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminUsers.profile(id),
    // Guard: id must be a positive integer — id ≤ 0 is a 400 from the backend
    // and indicates a missing/invalid route param. Disable rather than send.
    enabled: id > 0,
    queryFn: ({ signal }) =>
      client.get<AdminUserProfileDto>(`/api/Admin/Users/${id}`, { signal }),
  });
}
