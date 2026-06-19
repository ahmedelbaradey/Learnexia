/**
 * useModerationQueue — GET /api/Admin/Moderation/Queue (AdminOnly, paginated).
 *
 * Paginated, filterable queue of content moderation items.
 * Mirrors the `useSearchUsers` pattern: raw-path `client.getPaginated` with
 * PascalCase query params matching the backend `GetModerationQueueQuery` binding.
 *
 * IMPORTANT: uses the `adminModeration.*` query-key namespace — DISTINCT from all
 * other admin namespaces.
 *
 * Backend contract (P7-09, PR #180 merged):
 *   GET /api/Admin/Moderation/Queue
 *   Params: Status?, Source?, SubjectCode?, Grade?, DateFrom?, DateTo?, Search?,
 *           PageNumber (default 1), PageSize (default 20, backend clamps 1–100)
 *   Returns: BaseResponse<PaginatedResult<ModerationItemDto>>
 *   Auth: AdminOnly — anonymous → 401, non-admin → 403.
 *
 * Enum wire form: INTEGERS — backend has NO JsonStringEnumConverter.
 * ModerationStatus: Pending=0, Approved=1, Rejected=2, Flagged=3
 * ModerationSource: AiOutput=0, CurriculumUpload=1
 * Send and receive int values; map to/from string constants in the UI layer.
 *
 * NOTE: The design spec / brief originally recommended sending string names, but
 * the task description confirms "backend has NO JsonStringEnumConverter → enums
 * serialize as INTEGERS". The hooks use the MODERATION_STATUS / MODERATION_SOURCE
 * integer consts from @learnexia/shared and the wire filter params accept integers.
 *
 * Empty list returns a success envelope with an empty `data` array, not an error.
 * Page-size cap: backend clamps to 100; we enforce the same on the FE.
 *
 * PII-light invariant: carries `studentId` (a loose integer) + reason codes only,
 * never raw AI prompt/response content.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── Enum string unions (FE-local display types) ───────────────────────────────
// These string unions are used in FE component props / rendering logic.
// Wire values are integers — see MODERATION_STATUS / MODERATION_SOURCE in @learnexia/shared.

export type ModerationStatus = 'Pending' | 'Approved' | 'Rejected' | 'Flagged';
export type ModerationSource = 'AiOutput' | 'CurriculumUpload';

// ── DTO types (hand-written from backend DTOs — NOT NSwag-generated) ─────────

/**
 * List-item DTO for the moderation queue.
 * Carries no raw AI content — only reason codes, check names, and opaque refs.
 * `studentId` is a loose integer (no PII beyond the numeric id).
 * `safetyVerdict` is a JSON string — parse defensively via `SafetyVerdictView`.
 */
export interface ModerationItemDto {
  id: number;
  sourceEventId: string;          // GUID string
  source: ModerationSource;       // mapped from int on wire via MODERATION_SOURCE
  contentReference: string;       // opaque reference — always dir="ltr"
  studentId: number | null;
  taskKind: string | null;
  subjectCode: string | null;
  grade: number | null;
  status: ModerationStatus;       // mapped from int on wire via MODERATION_STATUS
  safetyVerdict: string;          // JSON string — parse defensively; never raw content
  detectedAt: string;             // ISO 8601
  createdAt: string;              // ISO 8601
}

/**
 * Detail DTO — extends ModerationItemDto with review fields.
 * Present when the item has been reviewed by an admin.
 */
export interface ModerationItemDetailDto extends ModerationItemDto {
  reviewedByUserId: number | null;
  reviewedAtUtc: string | null;
  reviewDecisionReason: string | null;
}

/**
 * Parsed shape of the `safetyVerdict` JSON string.
 * Keys are optional — parse defensively and treat absent/null arrays as empty.
 * The strings in these arrays are stable programmatic identifiers (not raw content).
 */
export interface SafetyVerdict {
  failedChecks?: string[];     // e.g. ["ToxicityCheck", "HarmfulContentCheck"]
  reasonCodes?: string[];      // e.g. ["TOXIC_LANGUAGE", "UNSAFE_TOPIC"]
  actionTaken?: string;        // "Blocked" | "Regenerated" | "FallbackReturned"
  modelId?: string;            // opaque model identifier — always dir="ltr"
}

// ── Filter type ───────────────────────────────────────────────────────────────

/**
 * Filters accepted by `useModerationQueue`.
 * PascalCase keys mirror the backend `GetModerationQueueQuery` binding.
 * Status and Source are sent as integer values matching the backend enums.
 */
export interface ModerationQueueFilters {
  /** Filter by moderation status (int: Pending=0 Approved=1 Rejected=2 Flagged=3). */
  Status?: number;
  /** Filter by source (int: AiOutput=0 CurriculumUpload=1). */
  Source?: number;
  /** Filter by subject code string (e.g. "Math", "Arabic"). */
  SubjectCode?: string;
  /** Filter by grade (1–12). */
  Grade?: number;
  /** ISO date-time — inclusive lower bound on DetectedAt. */
  DateFrom?: string;
  /** ISO date-time — inclusive upper bound on DetectedAt. */
  DateTo?: string;
  /** Partial match on ContentReference (server-side). */
  Search?: string;
  /** 1-based page number (default 1). */
  PageNumber?: number;
  /** Page size — max 100 per backend cap. */
  PageSize?: number;
}

const MAX_PAGE_SIZE = 100;

export function useModerationQueue(
  filters: ModerationQueueFilters = {},
): UseQueryResult<PaginatedResult<ModerationItemDto>, Error> {
  const client = useApiClient();
  const safePageSize =
    filters.PageSize !== undefined
      ? Math.min(filters.PageSize, MAX_PAGE_SIZE)
      : undefined;

  const normalised: ModerationQueueFilters = { ...filters, PageSize: safePageSize };

  return useQuery({
    queryKey: queryKeys.adminModeration.list(normalised),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<ModerationItemDto>('/api/Admin/Moderation/Queue', {
        query: {
          Status: filters.Status,
          Source: filters.Source,
          SubjectCode: filters.SubjectCode,
          Grade: filters.Grade,
          DateFrom: filters.DateFrom,
          DateTo: filters.DateTo,
          Search: filters.Search,
          PageNumber: filters.PageNumber,
          PageSize: safePageSize,
        },
        signal,
      }),
  });
}
