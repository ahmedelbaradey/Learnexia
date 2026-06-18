/**
 * useAdminQuestionsByLesson — GET /api/Learning/Questions/ByLesson/{lessonId}
 *
 * Returns all non-deleted questions for a lesson, ordered by SequenceOrder.
 * Includes inactive questions (admin view). Auth: AdminOnly.
 *
 * P7-04: questions key in adminCurriculum namespace.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AdminQuestionDto } from './curriculum.types';

export function useAdminQuestionsByLesson(
  lessonId: number,
): UseQueryResult<AdminQuestionDto[], Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.questions(lessonId),
    queryFn: ({ signal }) =>
      client.get<AdminQuestionDto[]>(
        `/api/Learning/Questions/ByLesson/${lessonId}`,
        { signal },
      ),
    enabled: lessonId > 0,
  });
}
