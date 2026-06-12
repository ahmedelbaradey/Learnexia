/**
 * useStudentAttempts — GET /api/Learning/Students/{studentId}/Attempts [Authorize]
 *
 * Attempt history for one student (A5 / CO-FE-7, design spec
 * `design-system/ui_kits/student-app/A5-attempt-history.md`). Shared by BOTH
 * surfaces: the child "My activity" screen passes its own id (`useMe`), the
 * parent Reports panel passes the active child's id (activeChildStore +
 * `useMyChildren` — never a free-form/user-supplied id).
 *
 * Authorization is SERVER-side. NOTE (2026-06-12): the backend handler today
 * authorizes ONLY the owning student (`studentId == JWT userId`); a parent↔child
 * link check is NOT yet implemented (deferred to a backend follow-up — see
 * HANDOFF "parent attempt-history authz"). Consequence: the parent Reports
 * attempts panel is FAIL-CLOSED (renders the generic error/empty state) until
 * that backend authz lands. The child "My activity" surface works today.
 * Do NOT "fix" this with a role-only bypass — that would be a Critical IDOR
 * (any parent reading any child). A 401/403/404 rejects like any other error —
 * callers render the generic error state and never branch on "exists but not
 * yours" (no IDOR leakage). `retry: false` so a denial is never retry-looped.
 *
 * The endpoint is unpaged today (spec gap G-3) — callers slice client-side.
 * Mirrors `useDashboard` (thin TanStack Query wrapper over the typed client).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { AttemptListItemDto } from '../schemas';

/**
 * Server `AttemptListItemDto.status` value set (Learning `AttemptStatus` enum
 * serialized to string by the backend mapper). Reference these — never raw
 * status strings — in app code.
 */
export const ATTEMPT_STATUS = {
  InProgress: 'InProgress',
  Completed: 'Completed',
  Abandoned: 'Abandoned',
} as const;

export type AttemptStatusValue =
  (typeof ATTEMPT_STATUS)[keyof typeof ATTEMPT_STATUS];

export function useStudentAttempts(
  studentId: number | undefined,
  options?: UseApiQueryOptions<AttemptListItemDto[]>,
): UseQueryResult<AttemptListItemDto[], Error> {
  const client = useTypedClient();
  return useQuery<AttemptListItemDto[], Error, AttemptListItemDto[]>({
    ...options,
    queryKey: queryKeys.learning.studentAttempts(studentId ?? null),
    queryFn: () =>
      unwrapEnvelope(client.attempts(studentId as number)).then(
        (data) => (data ?? []) as AttemptListItemDto[],
      ),
    enabled: studentId != null && (options?.enabled ?? true),
    // Security-UX: a 403/404 (not-linked / unknown student) must surface as
    // the generic error state immediately — never retry-loop a denial.
    retry: false,
  });
}
