/**
 * useSubmitAnswer — POST /api/Learning/Quizzes/{attemptId}/Answers
 *
 * Submits a student's answer for one question in a running attempt. Called
 * exactly once per question (enforced by the caller's locked-after-submit
 * state). Returns SubmitAnswerResponse (isCorrect, correctAnswer, hintAvailable).
 *
 * Auth: [Authorize(Roles="Student")] — child JWT required.
 */

import { useMutation, type UseMutationResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import type { SubmitAnswerCommand, SubmitAnswerResponse } from '../schemas';

export function useSubmitAnswer(attemptId: number): UseMutationResult<
  SubmitAnswerResponse,
  Error,
  SubmitAnswerCommand
> {
  const client = useTypedClient();

  return useMutation({
    mutationFn: (body: SubmitAnswerCommand): Promise<SubmitAnswerResponse> =>
      unwrapEnvelope(
        client.answers(attemptId, { ...body, attemptId }),
      ) as Promise<SubmitAnswerResponse>,
  });
}
