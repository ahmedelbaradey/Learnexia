/**
 * useUnitList — GET /api/learning/Units/List?SubjectId= (AdminOnly, paginated).
 *
 * List of units under a single subject. Always scoped to one subjectId —
 * units inherit language from the owning subject (no language column on units).
 *
 * Backend contract (P7-01):
 *   GET /api/learning/Units/List
 *   Params (PascalCase): SubjectId (required), PageNumber?, PageSize?, Search?, OrderBy?
 *   Returns: BaseResponse<PaginatedResult<UnitDto>>
 *   Auth: AdminOnly
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { UnitDto } from './curriculum.types';

export interface UnitListFilters {
  subjectId: number;
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  orderBy?: string;
}

const DEFAULT_PAGE_SIZE = 50;

export function useUnitList(
  filters: UnitListFilters,
): UseQueryResult<PaginatedResult<UnitDto>, Error> {
  const client = useApiClient();
  const pageSize = filters.pageSize ?? DEFAULT_PAGE_SIZE;

  const normalised = { pageNumber: filters.pageNumber, pageSize, search: filters.search };

  return useQuery({
    queryKey: queryKeys.adminCurriculum.units(filters.subjectId, normalised),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<UnitDto>('/api/learning/Units/List', {
        query: {
          SubjectId: filters.subjectId,
          PageNumber: filters.pageNumber,
          PageSize: pageSize,
          Search: filters.search,
          OrderBy: filters.orderBy,
        },
        signal,
      }),
  });
}
