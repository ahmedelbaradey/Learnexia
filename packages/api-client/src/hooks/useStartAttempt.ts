/**
 * useStartAttempt — POST /api/Learning/Quizzes/{lessonId}/Attempt
 *
 * Creates (or resumes) a quiz attempt for a lesson. Called explicitly on the
 * "Start lesson" CTA tap — not auto-fetched on mount. Returns the typed
 * StartAttemptResponse (attemptId + questions[]).
 *
 * Auth: [Authorize(Roles="Student")] — must be a child JWT (enforced by the
 * (child) route guard).
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { StartAttemptResponse } from '../schemas';

export interface StartAttemptInput {
  lessonId: number;
}

export function useStartAttempt(): UseMutationResult<
  StartAttemptResponse,
  Error,
  StartAttemptInput
> {
  const client = useTypedClient();

  return useMutation({
    mutationFn: ({ lessonId }: StartAttemptInput): Promise<StartAttemptResponse> =>
      unwrapEnvelope(client.attempt(lessonId)) as Promise<StartAttemptResponse>,
  });
}
