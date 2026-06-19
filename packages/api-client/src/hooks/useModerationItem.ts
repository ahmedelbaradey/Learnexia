/**
 * useModerationItem — GET /api/Admin/Moderation/{id} (AdminOnly).
 *
 * Fetches a single moderation item by numeric ID.
 * Returns ModerationItemDetailDto (includes review fields on top of list DTO).
 * 404 → the query errors; callers should render a not-found state.
 *
 * Mirrors `useAdminUserProfile` pattern: raw-path `client.get`, single item,
 * `adminModeration.item(id)` key. Invalidated by `useReviewModerationItem`.
 *
 * Backend contract (P7-09, PR #180 merged):
 *   GET /api/Admin/Moderation/{id:int}
 *   Returns: BaseResponse<ModerationItemDetailDto>
 *   404 when item not found.
 *   Auth: AdminOnly.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { ModerationItemDetailDto } from './useModerationQueue';

export function useModerationItem(
  id: number,
): UseQueryResult<ModerationItemDetailDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminModeration.item(id),
    queryFn: ({ signal }) =>
      client.get<ModerationItemDetailDto>(`/api/Admin/Moderation/${id}`, {
        signal,
      }),
    enabled: id > 0,
  });
}
