/**
 * useAdminGrades — GET /api/learning/Grades/List (AdminOnly, paginated).
 *
 * Fetches all grade levels for use in subject form grade selectors and the
 * coverage panel grade picker. Requests a large page size so all grades
 * (max 6) are returned in one page.
 *
 * Named `useAdminGrades` to avoid any conflict with a student-facing grades hook
 * that may exist or be added in other packages.
 *
 * Backend contract (P7-01):
 *   GET /api/learning/Grades/List
 *   Returns: BaseResponse<PaginatedResult<GradeDto>>
 *   Auth: public (checked — Grades/List has no AdminOnly attribute in the merged code)
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { GradeDto } from './curriculum.types';

export function useAdminGrades(): UseQueryResult<PaginatedResult<GradeDto>, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.grades(),
    staleTime: 5 * 60 * 1000, // grades rarely change; cache 5 min
    queryFn: ({ signal }) =>
      client.getPaginated<GradeDto>('/api/learning/Grades/List', {
        query: { PageSize: 20 },
        signal,
      }),
  });
}
