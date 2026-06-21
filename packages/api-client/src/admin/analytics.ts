/**
 * P7-10 Admin Platform Analytics hooks.
 *
 * Targets GET /api/Admin/Analytics/kpis (Identity module, AdminOnly).
 * Read-only — no mutations. All breakdowns are embedded in the KPI DTO;
 * there is NO /Analytics/trend endpoint this cycle (re-scope confirmed in brief).
 *
 * Param casing: `from`/`to` are camelCase (controller binding).
 * Returns `BaseResponse<PlatformKpiSummaryDto>` — client.get unwraps to the inner DTO.
 *
 * IMPORTANT: This hook is STRICTLY READ-ONLY. Do not pair with any mutation.
 * The analytics endpoint is aggregate-only — no per-child PII is exposed.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';

import { useApiClient } from '../hooks/apiClientContext';
import { queryKeys } from '../query/queryKeys';

// ── FE-local DTO types (hand-written from backend Dtos/Analytics) ──────────────

/** Subject-level breakdown inside `PlatformKpiSummaryDto.bySubject`. */
export interface SubjectBreakdown {
  /** Subject code int: 1=Math, 2=Science, 3=Arabic, 4=English. */
  subjectCode: number;
  /** Language int: 0=Arabic, 1=English. */
  language: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

/** Grade-level breakdown inside `PlatformKpiSummaryDto.byGrade`. */
export interface GradeBreakdown {
  gradeId: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

/** Language-level breakdown inside `PlatformKpiSummaryDto.byLanguage`. */
export interface LanguageBreakdown {
  /** Language int: 0=Arabic, 1=English. */
  language: number;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
}

/** Subscription plan breakdown inside `PlatformKpiSummaryDto.subscriptionsByTier`. */
export interface SubscriptionTierCount {
  planCode: string;
  count: number;
}

/**
 * Full KPI summary DTO returned by GET /api/Admin/Analytics/kpis.
 *
 * N/A facets: when an `*NaReason` field is a non-null, non-empty string, the FE
 * renders "N/A — {reason}" instead of the numeric value. This is first-class
 * design — do NOT fall back to showing `0` when a reason is present.
 *
 * Note: `quizzesCompletedNaReason` is a nullable string with NO sibling count
 * field — the quiz metric is always N/A whenever this string is present.
 */
export interface PlatformKpiSummaryDto {
  fromUtc: string;
  toUtc: string;
  lessonsCompleted: number;
  totalAttempts: number;
  distinctActiveStudents: number;
  /** When non-null/non-empty: quiz metric is N/A. There is no quizzesCompleted int. */
  quizzesCompletedNaReason: string | null;
  bySubject: SubjectBreakdown[];
  byGrade: GradeBreakdown[];
  byLanguage: LanguageBreakdown[];
  missionsCompleted: number;
  /** XP earned in the requested window. Long on the wire — JS number is safe for analytics. */
  xpEarnedInWindow: number;
  totalActiveSubscriptions: number;
  subscriptionsByTier: SubscriptionTierCount[];
  /** When non-null/non-empty: show alongside the subscription count card. */
  revenueNaReason: string | null;
  totalAiSafetyEvents: number;
  aiBlockedCount: number;
  aiFlaggedCount: number;
  aiRequestVolume: number;
  /** When non-null/non-empty: render "N/A — {reason}" on the AI requests card. */
  aiRequestVolumeNaReason: string | null;
  analyticsActiveStudents: number;
  totalSessions: number;
  /** Divide by 60 for minutes display. N/A when `sessionDurationNaReason` is set. */
  avgSessionDurationSeconds: number;
  /** 1 decimal. N/A when `retentionNaReason` is set (applies to avg-active-days too). */
  avgActiveDaysPerStudent: number;
  /** Multiply by 100 for %, 1 decimal. N/A when `retentionNaReason` is set. */
  returningStudentRate: number;
  /** When non-null/non-empty: returningStudentRate and avgActiveDays are N/A. */
  retentionNaReason: string | null;
  /** When non-null/non-empty: avgSessionDurationSeconds is N/A. */
  sessionDurationNaReason: string | null;
}

// ── Hook ───────────────────────────────────────────────────────────────────────

/**
 * Read-only KPI summary hook.
 *
 * Params `from`/`to` are sent camelCase (controller binding for
 * GET /api/Admin/Analytics/kpis). Both default to "last 30 days" server-side
 * when omitted. `keepPreviousData` keeps stale values visible during refetch
 * (smooth date-range filter transitions).
 *
 * IMPORTANT: This hook is read-only. Do not call any mutation from components
 * that use this hook.
 */
export function usePlatformKpis(
  from?: string,
  to?: string,
): UseQueryResult<PlatformKpiSummaryDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminAnalytics.kpis(from, to),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.get<PlatformKpiSummaryDto>('/api/Admin/Analytics/kpis', {
        query: { from, to },
        signal,
      }),
  });
}
