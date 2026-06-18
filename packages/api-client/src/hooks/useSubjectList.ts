/**
 * useSubjectList — GET /api/learning/Subjects/List (AdminOnly, paginated).
 *
 * Paginated, filterable list of subjects. Language filtering is client-side
 * (the backend has no `Language` query param — only GradeId/search/paging).
 *
 * Backend contract (P7-01):
 *   GET /api/learning/Subjects/List
 *   Params (PascalCase): GradeId?, PageNumber?, PageSize?, Search?, OrderBy?
 *   Returns: BaseResponse<PaginatedResult<SubjectDto>>
 *   Auth: AdminOnly
 *
 * Mirrors useSearchUsers pattern: raw-path getPaginated, keepPreviousData,
 * adminCurriculum.* query-key namespace.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { SubjectDto } from './curriculum.types';

export interface SubjectListFilters {
  gradeId?: number;
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  orderBy?: string;
}

const DEFAULT_PAGE_SIZE = 12;

export function useSubjectList(
  filters: SubjectListFilters = {},
): UseQueryResult<PaginatedResult<SubjectDto>, Error> {
  const client = useApiClient();
  const pageSize = filters.pageSize ?? DEFAULT_PAGE_SIZE;

  const normalised: SubjectListFilters = { ...filters, pageSize };

  return useQuery({
    queryKey: queryKeys.adminCurriculum.subjects(normalised),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<SubjectDto>('/api/learning/Subjects/List', {
        query: {
          GradeId: filters.gradeId,
          PageNumber: filters.pageNumber,
          PageSize: pageSize,
          Search: filters.search,
          OrderBy: filters.orderBy,
        },
        signal,
      }),
  });
}
