/**
 * useSearchUsers — GET /api/Admin/Users (admin-only, paginated).
 *
 * Paginated, filterable search over all users in the Identity store.
 * Mirrors the `useUserList.ts` pattern: raw-path `client.getPaginated` with
 * PascalCase query params matching the backend `SearchUsersQuery` binding.
 *
 * IMPORTANT: uses the `adminUsers.*` query-key namespace, NOT the legacy
 * `users.*` namespace — these are different endpoints with different DTOs.
 *
 * Backend contract (P7-06):
 *   GET /api/Admin/Users
 *   Params: Role? (string), Status? (int 0/1/2), Q? (free-text), PageNumber,
 *           PageSize (default 20, backend cap ≤100), OrderBy?
 *   Returns: PaginatedResult<AdminUserListItemDto>
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 *
 * Empty list returns a success envelope with an empty `data` array, not an
 * error — callers should render an "empty" state, not an error banner.
 *
 * Page-size cap: the backend clamps to 100; we enforce the same on the FE so
 * consumers don't accidentally request an unlimited page.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';

import type { PaginatedResult, AccountStatusValue } from '@learnexia/shared';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/Admin/AdminUserListItemDto.cs) ─

/**
 * Minimal list-item DTO for the admin user search.
 * Carries no child PII beyond name/email/role/status — grade/language/country
 * are omitted by design (only appear on the single-profile endpoint).
 */
export interface AdminUserListItemDto {
  /** User primary key. */
  id: number;
  fullName: string;
  email: string;
  /** Primary role name ("Parent" | "Student" | "Admin" | null). */
  role: string | null;
  /**
   * Governance lifecycle status: 0 Active, 1 Suspended, 2 Deleted.
   * Use `ACCOUNT_STATUS` const from `@learnexia/shared` for comparisons.
   */
  accountStatus: AccountStatusValue;
  /** UTC timestamp when the account was created (ISO 8601 string on the wire). */
  createdAt: string;
}

// ── Hook ───────────────────────────────────────────────────────────────────────

/** Filters accepted by `useSearchUsers`. All params are optional. */
export interface SearchUsersFilters {
  /** Filter by primary role. Pass "Parent" or "Student" (Admin/SuperAdmin excluded per D6). */
  role?: string;
  /** Filter by account status (ACCOUNT_STATUS numeric value). */
  status?: AccountStatusValue;
  /** Free-text search over full name + email. */
  q?: string;
  /** 1-based page number (default 1). */
  pageNumber?: number;
  /** Page size — max 100 per backend cap. */
  pageSize?: number;
  /** Optional sort column. Append "desc" suffix for descending (backend convention). */
  orderBy?: string;
}

const MAX_PAGE_SIZE = 100;

export function useSearchUsers(
  filters: SearchUsersFilters = {},
): UseQueryResult<PaginatedResult<AdminUserListItemDto>, Error> {
  const client = useApiClient();
  const safePageSize =
    filters.pageSize !== undefined
      ? Math.min(filters.pageSize, MAX_PAGE_SIZE)
      : undefined;

  const normalised: SearchUsersFilters = { ...filters, pageSize: safePageSize };

  return useQuery({
    queryKey: queryKeys.adminUsers.list(normalised),
    // Keep previous page data visible while the next page loads (smooth pagination).
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<AdminUserListItemDto>('/api/Admin/Users', {
        // PascalCase params mirror the backend SearchUsersQuery binding.
        query: {
          Role: filters.role,
          Status: filters.status,
          Q: filters.q,
          PageNumber: filters.pageNumber,
          PageSize: safePageSize,
          OrderBy: filters.orderBy,
        },
        signal,
      }),
  });
}
