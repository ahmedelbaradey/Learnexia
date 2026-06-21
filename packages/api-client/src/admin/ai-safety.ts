/**
 * P7-11 Admin AI-Safety Monitoring hooks.
 *
 * Targets /api/Admin/AiSafety/* (Ai module, AdminOnly). All read-only.
 * Param casing: PascalCase `From`/`To` for all date params (controller binding).
 * `useEvalResults` takes NO params. `useFlaggedOutputs` uses `client.getPaginated`.
 *
 * CRITICAL INVARIANTS:
 * 1. READ-ONLY — no mutation hooks.
 * 2. `studentId` is in `FlaggedOutputDto` but MUST NEVER be rendered on screen.
 * 3. No raw prompt/response text is returned by the API. PII-light by design.
 * 4. Bootstrap sentinel for evals: runId === Guid.Empty AND totalCases === 0
 *    → "no eval run yet" state, NOT a real threshold breach.
 * 5. `useSafetyTrend` returns a bare `SafetyTrendBucketDto[]` (not a wrapped object).
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';

import type { PaginatedResult } from '@learnexia/shared';

import { useApiClient } from '../hooks/apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from verified backend DTOs) ───────────────

/** Generic count breakdown used in signals and flagged filter options. */
export interface CountBreakdownDto {
  label: string;
  count: number;
}

/** Safety signal summary — GET /api/Admin/AiSafety/signals. */
export interface SafetySignalSummaryDto {
  from: string;
  to: string;
  totalEvents: number;
  blockedCount: number;
  /** Double 0–1 — multiply by 100 for % display. */
  blockedRate: number;
  regeneratedCount: number;
  regeneratedRate: number;
  fallbackReturnedCount: number;
  fallbackReturnedRate: number;
  breakdownByAction: CountBreakdownDto[];
  breakdownByReasonCode: CountBreakdownDto[];
  breakdownByModelId: CountBreakdownDto[];
  breakdownByTaskKind: CountBreakdownDto[];
}

/**
 * Safety trend bucket — GET /api/Admin/AiSafety/trend returns bare list of these.
 * `bucketDate` is an ISO date string (one bucket per day).
 */
export interface SafetyTrendBucketDto {
  bucketDate: string;
  totalCount: number;
  blockedCount: number;
  regeneratedCount: number;
  fallbackReturnedCount: number;
}

/** Usage breakdown by model — nested inside TutorUsageDto. */
export interface TutorUsageByModelDto {
  modelId: string;
  calls: number;
  totalTokens: number;
  totalEstimatedCostUsd: number;
}

/** Usage breakdown by task kind — nested inside TutorUsageDto. */
export interface TutorUsageByTaskKindDto {
  taskKind: string;
  calls: number;
  totalTokens: number;
  totalEstimatedCostUsd: number;
}

/** Daily cost trend point — nested inside TutorUsageDto.trend. */
export interface TutorUsageTrendDto {
  date: string;
  calls: number;
  totalEstimatedCostUsd: number;
}

/** Tutor usage summary — GET /api/Admin/AiSafety/usage. */
export interface TutorUsageDto {
  from: string;
  to: string;
  totalCalls: number;
  totalPromptTokens: number;
  totalCompletionTokens: number;
  /** decimal on wire — number in TS. */
  totalEstimatedCostUsd: number;
  avgLatencyMs: number;
  /** double 0–1 — multiply by 100 for % display. */
  cacheHitRate: number;
  byModel: TutorUsageByModelDto[];
  byTaskKind: TutorUsageByTaskKindDto[];
  trend: TutorUsageTrendDto[];
}

/** Per-check/subject/language breakdown inside EvalResultsDto. */
export interface EvalCheckBreakdownDto {
  passed: number;
  total: number;
  /** 0–100. */
  passRate: number;
}

/**
 * Eval results — GET /api/Admin/AiSafety/evals (no params).
 *
 * BOOTSTRAP SENTINEL: when `runId === '00000000-0000-0000-0000-000000000000'`
 * AND `totalCases === 0`, this is the bootstrap placeholder — no eval has been
 * run yet. Render the "no eval run yet" state; do NOT show the breach banner
 * even though `breached` will be `true` in the sentinel.
 */
export interface EvalResultsDto {
  runId: string;
  ranAt: string;
  totalCases: number;
  passedCases: number;
  failedCases: number;
  /** 0–100. */
  passRate: number;
  failRate: number;
  thresholdPercent: number;
  breached: boolean;
  tier: string;
  note: string;
  byCheck: Record<string, EvalCheckBreakdownDto>;
  bySubject: Record<string, EvalCheckBreakdownDto>;
  byLanguage: Record<string, EvalCheckBreakdownDto>;
}

/**
 * Flagged output DTO — GET /api/Admin/AiSafety/flagged (paginated).
 *
 * CRITICAL: `studentId` MUST NEVER be rendered on screen. It is in the DTO
 * type for completeness but the FE treats it as opaque/unused. Only the
 * `contentRef` (SafetyEvent.Id — an opaque integer, not a student identifier)
 * is shown as a reference ID.
 */
export interface FlaggedOutputDto {
  /** Opaque reference ID (SafetyEvent.Id). NOT a student identifier. */
  contentRef: number;
  taskKind: string;
  actionTaken: string;
  reasonCodes: string[];
  failedChecks: string[];
  modelId: string;
  /** Present in DTO but NEVER rendered on screen (PII-light invariant). */
  studentId: number | null;
  occurredAtUtc: string;
}

/** Filters for useFlaggedOutputs — all PascalCase to match controller binding. */
export interface FlaggedOutputsFilters {
  Action?: string;
  ReasonCode?: string;
  TaskKind?: string;
  From?: string;
  To?: string;
  PageNumber?: number;
  PageSize?: number;
}

const MAX_PAGE_SIZE = 100;
const GUID_EMPTY = '00000000-0000-0000-0000-000000000000';

// ── Sentinel detection helper (exported for component use) ─────────────────────

/**
 * Returns `true` when the eval DTO is the bootstrap sentinel (no eval has run yet).
 * Detection: `runId === Guid.Empty && totalCases === 0`.
 * When the sentinel is detected, do NOT show the breach banner.
 */
export function isEvalBootstrapSentinel(dto: EvalResultsDto): boolean {
  return dto.runId === GUID_EMPTY && dto.totalCases === 0;
}

// ── Hooks ──────────────────────────────────────────────────────────────────────

/**
 * Safety signal summary — GET /api/Admin/AiSafety/signals?From=&To=.
 * No subject/language params or breakdowns (SafetyEvent has no such columns).
 */
export function useSafetySignals(
  From?: string,
  To?: string,
): UseQueryResult<SafetySignalSummaryDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminAiSafety.signals(From, To),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.get<SafetySignalSummaryDto>('/api/Admin/AiSafety/signals', {
        query: { From, To },
        signal,
      }),
  });
}

/**
 * Safety trend — GET /api/Admin/AiSafety/trend?From=&To=.
 * Returns a bare list of `SafetyTrendBucketDto` (not a wrapped object).
 * The `client.get` call receives `SafetyTrendBucketDto[]` directly.
 */
export function useSafetyTrend(
  From?: string,
  To?: string,
): UseQueryResult<SafetyTrendBucketDto[], Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminAiSafety.trend(From, To),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.get<SafetyTrendBucketDto[]>('/api/Admin/AiSafety/trend', {
        query: { From, To },
        signal,
      }),
  });
}

/**
 * Tutor usage + cost — GET /api/Admin/AiSafety/usage?From=&To=.
 */
export function useTutorUsage(
  From?: string,
  To?: string,
): UseQueryResult<TutorUsageDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminAiSafety.usage(From, To),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.get<TutorUsageDto>('/api/Admin/AiSafety/usage', {
        query: { From, To },
        signal,
      }),
  });
}

/**
 * Eval results — GET /api/Admin/AiSafety/evals (NO params).
 *
 * Returns 200 with a bootstrap sentinel when no eval run artifact is present.
 * Use `isEvalBootstrapSentinel(data)` to detect this state.
 */
export function useEvalResults(): UseQueryResult<EvalResultsDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminAiSafety.evals(),
    queryFn: ({ signal }) =>
      client.get<EvalResultsDto>('/api/Admin/AiSafety/evals', { signal }),
  });
}

/**
 * Flagged outputs — GET /api/Admin/AiSafety/flagged (paginated).
 *
 * Clones `useAuditLog` pattern: `client.getPaginated`, PascalCase params,
 * `keepPreviousData`, page-size clamp ≤ 100.
 *
 * CRITICAL: `studentId` in `FlaggedOutputDto` MUST NEVER be rendered on screen.
 */
export function useFlaggedOutputs(
  filters: FlaggedOutputsFilters = {},
): UseQueryResult<PaginatedResult<FlaggedOutputDto>, Error> {
  const client = useApiClient();

  const safePageSize =
    filters.PageSize !== undefined
      ? Math.min(filters.PageSize, MAX_PAGE_SIZE)
      : undefined;

  const normalised: FlaggedOutputsFilters = { ...filters, PageSize: safePageSize };

  return useQuery({
    queryKey: queryKeys.adminAiSafety.flagged(normalised),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<FlaggedOutputDto>('/api/Admin/AiSafety/flagged', {
        query: {
          Action: filters.Action,
          ReasonCode: filters.ReasonCode,
          TaskKind: filters.TaskKind,
          From: filters.From,
          To: filters.To,
          PageNumber: filters.PageNumber,
          PageSize: safePageSize,
        },
        signal,
      }),
  });
}
