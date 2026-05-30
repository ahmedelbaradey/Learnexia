/**
 * useSubjectsForGrade — GET /api/learning/Subjects/ForGrade?grade={n}
 *
 * Disabled when grade is undefined or outside the valid 1..6 range.
 * The endpoint is anonymously callable on the BE (no [Authorize]), but Wave 11
 * only renders for signed-in students. Returns the unwrapped list of
 * StudentSubjectDto or an empty array when the query is disabled.
 *
 * Mirror of useMe pattern: thin TanStack Query wrapper → unwrapEnvelope.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { StudentSubjectDto } from '../schemas';

export function useSubjectsForGrade(
  grade: number | undefined,
  options?: UseApiQueryOptions<StudentSubjectDto[]>,
): UseQueryResult<StudentSubjectDto[], Error> {
  const client = useTypedClient();
  const isGradeValid =
    grade !== undefined && grade >= 1 && grade <= 6;

  return useQuery<StudentSubjectDto[], Error, StudentSubjectDto[]>({
    ...options,
    queryKey: queryKeys.learning.subjectsForGrade(grade),
    enabled: isGradeValid && (options?.enabled !== false),
    queryFn: () =>
      unwrapEnvelope(client.forGrade(grade)).then(
        (data) => data ?? [],
      ) as Promise<StudentSubjectDto[]>,
  });
}
