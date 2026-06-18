/**
 * useAdminQuestion — GET /api/Learning/Questions/{id}
 *
 * Returns a single question with full detail (including CorrectAnswer).
 * Auth: AdminOnly.
 *
 * P7-04: question(id) key in adminCurriculum namespace.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AdminQuestionDto } from './curriculum.types';

export function useAdminQuestion(
  id: number,
): UseQueryResult<AdminQuestionDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.question(id),
    queryFn: ({ signal }) =>
      client.get<AdminQuestionDto>(`/api/Learning/Questions/${id}`, { signal }),
    enabled: id > 0,
  });
}
