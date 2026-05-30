/**
 * useLesson — GET /api/learning/Lessons/{id}
 *
 * Fetches the single-lesson detail (explanation, visual, quickCheck) for the
 * lesson intro stage. Disabled when lessonId is 0 or negative (invalid / not
 * yet resolved). Auth: [Authorize] (any role — child JWT on the (child) route).
 *
 * Mirror of useSubjectLessons pattern: thin TanStack Query wrapper → unwrapEnvelope.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { SingleLessonResponse } from '../schemas';

export function useLesson(
  lessonId: number,
  options?: UseApiQueryOptions<SingleLessonResponse>,
): UseQueryResult<SingleLessonResponse, Error> {
  const client = useTypedClient();

  return useQuery<SingleLessonResponse, Error, SingleLessonResponse>({
    ...options,
    queryKey: queryKeys.learning.lesson(lessonId),
    enabled: lessonId > 0 && (options?.enabled !== false),
    queryFn: () =>
      unwrapEnvelope(client.lessonsGET(lessonId)) as Promise<SingleLessonResponse>,
  });
}
