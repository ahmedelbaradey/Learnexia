/**
 * useUserFamily — GET /api/Admin/Users/{id}/family (admin-only).
 *
 * Family linkage for a single user.
 *   - For a parent: `children[]` is populated; `parents[]` is empty.
 *   - For a child:  `parents[]` is populated; `children[]` is empty.
 *
 * Family data is sourced from the Parent module via `IParentChildQuery` seam
 * (no cross-module FK — clean module boundaries are preserved).
 *
 * PRIVACY NOTE: `email` in `AdminFamilyMemberDto` is null for child members by
 * design (Finding #6 in the backend DTO). An admin needing a child's email must
 * open the child's individual profile endpoint. The family panel must NOT show
 * child emails; it shows name + grade + a deep-link to `/users/[id]`.
 *
 * Backend contract (P7-06):
 *   GET /api/Admin/Users/{id}/family
 *   Returns: BaseResponse<AdminFamilyDto>
 *   Not found → 404.
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/Admin/AdminFamilyDto.cs) ─

/**
 * Minimal family member summary for admin triage.
 * `email` is non-null for parent members only; always null for children.
 */
export interface AdminFamilyMemberDto {
  id: number;
  fullName: string;
  /** Non-null for parent members; null for child members (privacy by design). */
  email: string | null;
  /** Grade 1–6 for child members; null for parent members. */
  grade: number | null;
}

/** Family linkage for a single user (parent or child). */
export interface AdminFamilyDto {
  userId: number;
  /** Role of the queried user ("Parent" | "Student"). */
  userRole: string;
  /** Populated when `userRole === "Parent"`. */
  children: AdminFamilyMemberDto[];
  /** Populated when `userRole === "Student"`. */
  parents: AdminFamilyMemberDto[];
}

// ── Hook ───────────────────────────────────────────────────────────────────────

export function useUserFamily(
  id: number,
): UseQueryResult<AdminFamilyDto, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.adminUsers.family(id),
    enabled: id > 0,
    queryFn: ({ signal }) =>
      client.get<AdminFamilyDto>(`/api/Admin/Users/${id}/family`, { signal }),
  });
}
