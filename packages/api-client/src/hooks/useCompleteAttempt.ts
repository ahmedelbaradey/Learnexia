/**
 * useCompleteAttempt — POST /api/Learning/Quizzes/{attemptId}/Complete
 *
 * Marks a quiz attempt as Completed and retrieves the AttemptSummaryDto.
 * Called after the last question is answered (correct or incorrect) and the
 * student has advanced past the final feedback strip.
 *
 * Auth: [Authorize(Roles="Student")] — child JWT required.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { AttemptSummaryDto } from '../schemas';

export interface CompleteAttemptInput {
  attemptId: number;
}

export function useCompleteAttempt(): UseMutationResult<
  AttemptSummaryDto,
  Error,
  CompleteAttemptInput
> {
  const client = useTypedClient();

  return useMutation({
    mutationFn: ({ attemptId }: CompleteAttemptInput): Promise<AttemptSummaryDto> =>
      unwrapEnvelope(client.complete(attemptId)) as Promise<AttemptSummaryDto>,
  });
}
