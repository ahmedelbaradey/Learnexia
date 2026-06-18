/**
 * useLessonsByUnit — GET /api/learning/Lessons/List?UnitId={unitId} (AdminOnly, paginated).
 *
 * Returns all lessons (incl. inactive) under a unit ordered by sequenceOrder.
 * Mirrors the useSearchUsers pattern: raw-path client.getPaginated, PascalCase params.
 *
 * Backend contract (P7-02):
 *   GET /api/learning/Lessons/List
 *   Params: UnitId (int, optional filter), PageNumber, PageSize, OrderBy?
 *   Returns: BaseResponse<PaginatedResult<SingleLessonResponse>>
 *   Auth: AdminOnly.
 *
 * Empty list is a success-with-empty-data state, not an error.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AdminSingleLessonResponse } from './curriculum.types';

export interface LessonListFilters {
  pageNumber?: number;
  pageSize?: number;
  orderBy?: string;
}

export function useLessonsByUnit(
  unitId: number,
  filters: LessonListFilters = {},
): UseQueryResult<PaginatedResult<AdminSingleLessonResponse>, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.lessons(unitId, filters),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<AdminSingleLessonResponse>('/api/learning/Lessons/List', {
        query: {
          UnitId: unitId,
          PageNumber: filters.pageNumber,
          PageSize: filters.pageSize,
          OrderBy: filters.orderBy,
        },
        signal,
      }),
  });
}
