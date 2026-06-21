/**
 * useMyTimedEventParticipations — GET /api/Gamification/TimedEventParticipations/Me
 * [Authorize] — student resolved from JWT only; IDOR-proof by construction.
 *
 * Returns the current student's active timed-event participation snapshots.
 * Empty array = not yet participating in any active event (participation is
 * created lazily on first qualifying contribution — this is the "join by playing"
 * state; there is simply NO row for events the child hasn't contributed to yet).
 *
 * FE-local DTO types (hand-authored per brief — the endpoint and DTO are absent
 * from the generated NSwag client so we use the raw `useApiClient().get<T>()`
 * pattern instead of `useTypedClient()`. Live endpoint + field casing verified
 * against Swagger v2 (/tmp/swagger_v2.json) and live curl response.
 *
 * P4-12-FE-1. Pattern: packages/api-client/src/hooks/useMyMissions.ts.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useApiClient } from '../hooks/apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from '../hooks/useApiQuery';

/* ------------------------------------------------------------------ */
/* FE-local DTO types (hand-authored — not in generated client)        */
/* ------------------------------------------------------------------ */

/**
 * Participation status — mirrors the backend `TimedEventParticipationStatusDto`
 * numeric enum. Use this enum; never compare against the raw integer.
 */
export enum TimedEventParticipationStatus {
  NotStarted = 0,
  InProgress = 1,
  Completed = 2,
}

/**
 * Per-event participation snapshot for the current student.
 * Returned from `GET /api/Gamification/TimedEventParticipations/Me`.
 *
 * There is NO localized name or reward amount in this DTO.
 * The localized name/multiplier come from `ActiveTimedEventDto` (dashboard).
 * Join the two by `code` to render a complete timed-event card.
 */
export interface TimedEventParticipationSnapshot {
  /** Opaque FK — not rendered; exists for traceability. */
  timedEventId: number;
  /** Stable event code (e.g. "DOUBLE_XP_WEEKEND") — join key to ActiveTimedEventDto.code. */
  code: string | null;
  /** How many qualifying actions the student has completed in this window. */
  progress: number;
  /** Denormalized target at participation creation time. */
  target: number;
  /** Participation status: 0 = NotStarted, 1 = InProgress, 2 = Completed. */
  status: TimedEventParticipationStatus;
  /** UTC timestamp when the student first contributed (row created). */
  joinedUtc: string;
  /** UTC timestamp of completion; null when not yet completed. */
  completedUtc: string | null;
  /** UTC timestamp of the event window end (denormalized). */
  eventEndUtc: string;
}

/* ------------------------------------------------------------------ */
/* Hook                                                                 */
/* ------------------------------------------------------------------ */

/**
 * Fetches the current student's timed-event participation snapshots.
 *
 * Returns `TimedEventParticipationSnapshot[]` (empty array when the student
 * has not yet participated in any active timed event — the FE derives the
 * "join by playing" state by looking for events present in
 * `useDashboard().activeTimedEvents` that have no matching participation row).
 *
 * @example
 *   const { data: participations = [] } = useMyTimedEventParticipations();
 *   const snapMap = new Map(participations.map(s => [s.code, s]));
 *   // For each active event: snapMap.get(event.code) → snapshot or undefined.
 */
export function useMyTimedEventParticipations(
  options?: UseApiQueryOptions<TimedEventParticipationSnapshot[]>,
): UseQueryResult<TimedEventParticipationSnapshot[], Error> {
  const client = useApiClient();
  return useQuery<TimedEventParticipationSnapshot[], Error, TimedEventParticipationSnapshot[]>({
    ...options,
    queryKey: queryKeys.gamification.timedEventParticipations(),
    queryFn: () =>
      client.get<TimedEventParticipationSnapshot[]>(
        '/api/Gamification/TimedEventParticipations/Me',
      ),
  });
}
