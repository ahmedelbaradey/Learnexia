/**
 * useUserList — paginated query example using `PaginatedResult`.
 *
 * Pattern for paged lists: pass filters/paging as query params, call
 * `client.getPaginated` (returns the full envelope with `currentPage`,
 * `totalCount`, `totalPages`, `pageSize`), and key the query on the filters.
 *
 * `keepPreviousData`-style smoothing is left to the caller via
 * `placeholderData` if desired; this hook establishes the data flow.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import type { PaginatedResult } from '@learnexia/shared';

import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { GetUserListResponse } from '../schemas';

export interface UserListFilters {
  role?: string;
  isActive?: boolean;
  name?: string;
  pageNumber?: number;
  pageSize?: number;
  search?: string;
  orderBy?: string;
}

export function useUserList(
  filters: UserListFilters = {},
): UseQueryResult<PaginatedResult<GetUserListResponse>, Error> {
  const client = useApiClient();
  return useQuery({
    queryKey: queryKeys.users.list(filters),
    queryFn: ({ signal }) =>
      client.getPaginated<GetUserListResponse>(
        '/api/Users/UserManagement/UserList',
        {
          // Backend query params use PascalCase (Role, IsActive, PageNumber…).
          query: {
            Role: filters.role,
            IsActive: filters.isActive,
            Name: filters.name,
            PageNumber: filters.pageNumber,
            PageSize: filters.pageSize,
            Search: filters.search,
            OrderBy: filters.orderBy,
          },
          signal,
        },
      ),
  });
}
