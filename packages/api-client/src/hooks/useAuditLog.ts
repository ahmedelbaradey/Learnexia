/**
 * useAuditLog — GET /api/Admin/Audit/Log (admin-only, paginated, read-only).
 *
 * Paginated, filterable read of the admin governance audit log.
 * Mirrors `useSearchUsers.ts` exactly: raw-path `client.getPaginated`,
 * PascalCase query params matching the backend `GetAuditLogQuery` binding,
 * `placeholderData: keepPreviousData`, page-size clamp ≤ 100.
 *
 * Backend contract (P7-12):
 *   GET /api/Admin/Audit/Log
 *   Params: AdminUserId? (int), ActionType? (string), TargetEntityType? (string),
 *           DateFrom? (ISO), DateTo? (ISO), PageNumber (default 1),
 *           PageSize (default 20, backend cap ≤ 100).
 *   Returns: BaseResponse<PaginatedResult<AuditLogDto>>
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 *
 * IMPORTANT:
 * - This is a STRICTLY READ-ONLY hook. It MUST NOT be paired with any mutation
 *   hook or useApiMutation call. The audit log is immutable.
 * - `details` is a single string (possibly JSON) — NOT before/after. Render
 *   as plain text / pretty-printed JSON. NEVER as HTML.
 * - There is NO export endpoint in v1. Do not add one here.
 * - FE type is hand-written (no NSwag) — sourced from the merged
 *   `AuditLogDto.cs` in the Moderation module.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';

import type { PaginatedResult } from '@learnexia/shared';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/AuditLog/AuditLogDto.cs) ─

/**
 * Single audit-log entry DTO.
 *
 * All fields are PII-safe: only entity ids + enum/state values are stored.
 * `details` may be null or a JSON string that the FE pretty-prints; never HTML.
 * Timestamps are ISO 8601 strings. `targetEntityId === 0` signals a batch/
 * multi-row action with no single target entity.
 */
export interface AuditLogDto {
  /** Row primary key. */
  id: number;
  /** Idempotency / deduplication key (GUID string). Always `dir="ltr"`. */
  eventId: string;
  /** Primary key of the admin who performed the action. Always `dir="ltr"`. */
  adminUserId: number;
  /** AdminActions.* constant string, e.g. "Subject.Created". */
  action: string;
  /** Entity type string, e.g. "Subject", "User", "Child". */
  targetEntityType: string;
  /**
   * Primary key of the affected entity. `0` signals a batch/multi-row action
   * (no single target) — the UI must hide the `#0` chip in that case.
   */
  targetEntityId: number;
  /**
   * PII-safe summary (ids + enum/state values only). May be parseable JSON
   * (pretty-print it) or plain text. Must NEVER be rendered as HTML.
   * Null when no extra context was recorded.
   */
  details: string | null;
  /** UTC timestamp of the admin action itself (display this as "When"). ISO 8601. */
  occurredAtUtc: string;
  /** UTC timestamp of when the audit row was inserted. ISO 8601. */
  createdAt: string;
}

// ── Filter type ────────────────────────────────────────────────────────────────

/** Filters accepted by `useAuditLog`. All params are optional. */
export interface AuditLogFilters {
  /** Filter by acting admin's user id. */
  AdminUserId?: number;
  /** Filter by action type — exact string from AdminActions.* constants. */
  ActionType?: string;
  /** Filter by target entity type, e.g. "Subject", "User". */
  TargetEntityType?: string;
  /**
   * Inclusive lower bound on OccurredAtUtc (ISO 8601).
   * Send as "YYYY-MM-DDT00:00:00Z" from date-only input.
   */
  DateFrom?: string;
  /**
   * Inclusive upper bound on OccurredAtUtc (ISO 8601).
   * Send as "YYYY-MM-DDT23:59:59Z" from date-only input (inclusive end-of-day).
   */
  DateTo?: string;
  /** 1-based page number (default 1). */
  PageNumber?: number;
  /** Page size — max 100 per backend cap. */
  PageSize?: number;
}

const MAX_PAGE_SIZE = 100;

// ── Hook ───────────────────────────────────────────────────────────────────────

/**
 * Read-only paginated audit-log hook.
 *
 * Returns the standard UseQueryResult carrying `PaginatedResult<AuditLogDto>`.
 * Uses `keepPreviousData` so rows stay visible during refetch (smooth
 * pagination / filter transitions). Page-size is clamped to ≤ 100 on the FE
 * to match the backend clamp.
 *
 * IMPORTANT: This hook is read-only. Do not call any mutation from a component
 * that uses this hook.
 */
export function useAuditLog(
  filters: AuditLogFilters = {},
): UseQueryResult<PaginatedResult<AuditLogDto>, Error> {
  const client = useApiClient();

  const safePageSize =
    filters.PageSize !== undefined
      ? Math.min(filters.PageSize, MAX_PAGE_SIZE)
      : undefined;

  const normalised: AuditLogFilters = { ...filters, PageSize: safePageSize };

  return useQuery({
    queryKey: queryKeys.adminAudit.list(normalised),
    // Keep previous page data visible while the next page loads.
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<AuditLogDto>('/api/Admin/Audit/Log', {
        // PascalCase params mirror the backend GetAuditLogQuery binding.
        query: {
          AdminUserId: filters.AdminUserId,
          ActionType: filters.ActionType,
          TargetEntityType: filters.TargetEntityType,
          DateFrom: filters.DateFrom,
          DateTo: filters.DateTo,
          PageNumber: filters.PageNumber,
          PageSize: safePageSize,
        },
        signal,
      }),
  });
}
