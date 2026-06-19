/**
 * useSkillList — GET /api/learning/Skills/List (AdminOnly, paginated).
 *
 * Paginated, filterable list of skills. The backend supports optional ConceptId,
 * Search, and standard paging params (PascalCase via ApiClient.buildUrl).
 *
 * Backend contract (P7-03):
 *   GET /api/learning/Skills/List
 *   Params (PascalCase): ConceptId?, PageNumber?, PageSize?, Search?, OrderBy?
 *   Returns: BaseResponse<PaginatedResult<SingleSkillResponse>>
 *   Auth: AdminOnly
 *
 * Mirrors useSubjectList / useSearchUsers pattern: raw-path getPaginated,
 * keepPreviousData, adminCurriculum.* query-key namespace.
 */

import { useQuery, keepPreviousData, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { SingleSkillResponse } from './curriculum.types';

export interface SkillListFilters {
  conceptId?: number;
  search?: string;
  pageNumber?: number;
  pageSize?: number;
  orderBy?: string;
}

const DEFAULT_PAGE_SIZE = 20;

export function useSkillList(
  filters: SkillListFilters = {},
): UseQueryResult<PaginatedResult<SingleSkillResponse>, Error> {
  const client = useApiClient();
  const pageSize = filters.pageSize ?? DEFAULT_PAGE_SIZE;

  const normalised: SkillListFilters = { ...filters, pageSize };

  return useQuery({
    queryKey: queryKeys.adminCurriculum.skills(normalised),
    placeholderData: keepPreviousData,
    queryFn: ({ signal }) =>
      client.getPaginated<SingleSkillResponse>('/api/learning/Skills/List', {
        query: {
          ConceptId: filters.conceptId,
          PageNumber: filters.pageNumber,
          PageSize: pageSize,
          Search: filters.search,
          OrderBy: filters.orderBy,
        },
        signal,
      }),
  });
}
