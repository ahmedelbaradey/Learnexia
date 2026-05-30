/**
 * useAbandonAttempt — POST /api/Learning/Quizzes/{attemptId}/Abandon
 *
 * Marks a quiz attempt as Abandoned. Called fire-and-forget from the screen's
 * unmount cleanup when the student backs out mid-quiz. Idempotent on terminal
 * attempts (BE accepts the call even if already Completed/Abandoned).
 *
 * Auth: [Authorize(Roles="Student")] — child JWT required.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { AttemptSummaryDto } from '../schemas';

export interface AbandonAttemptInput {
  attemptId: number;
}

export function useAbandonAttempt(): UseMutationResult<
  AttemptSummaryDto,
  Error,
  AbandonAttemptInput
> {
  const client = useTypedClient();

  return useMutation({
    mutationFn: ({ attemptId }: AbandonAttemptInput): Promise<AttemptSummaryDto> =>
      unwrapEnvelope(client.abandon(attemptId)) as Promise<AttemptSummaryDto>,
  });
}
