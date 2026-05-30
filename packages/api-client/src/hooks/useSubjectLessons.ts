/**
 * useSubjectLessons — GET /api/learning/Subjects/{id}/Lessons
 *
 * Auth-required: 401 on unauthenticated. The existing api-client interceptor
 * handles refresh; if refresh fails the auth-route guard bounces to login.
 * Disabled when subjectId is 0 or negative (invalid / not yet resolved).
 *
 * Mirror of useMe pattern: thin TanStack Query wrapper → unwrapEnvelope.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { UnitWithLessonsDto } from '../schemas';

export function useSubjectLessons(
  subjectId: number,
  options?: UseApiQueryOptions<UnitWithLessonsDto[]>,
): UseQueryResult<UnitWithLessonsDto[], Error> {
  const client = useTypedClient();

  return useQuery<UnitWithLessonsDto[], Error, UnitWithLessonsDto[]>({
    ...options,
    queryKey: queryKeys.learning.subjectLessons(subjectId),
    enabled: subjectId > 0 && (options?.enabled !== false),
    queryFn: () =>
      unwrapEnvelope(client.lessonsGET3(subjectId)).then(
        (data) => data ?? [],
      ) as Promise<UnitWithLessonsDto[]>,
  });
}
