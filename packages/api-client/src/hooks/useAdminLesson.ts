/**
 * useAdminLesson — GET /api/learning/Lessons/{id}/Admin (AdminOnly).
 *
 * Returns the full lesson metadata PLUS all non-deleted content blocks ordered
 * by sequenceOrder. This is the primary read for the content-block editor —
 * prefer this over a separate blocks fetch for consistency.
 *
 * Backend contract (P7-02):
 *   GET /api/learning/Lessons/{id}/Admin
 *   Returns: BaseResponse<AdminLessonDetailDto>
 *   Auth: AdminOnly.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AdminLessonDetailDto } from './curriculum.types';

export function useAdminLesson(
  lessonId: number,
): UseQueryResult<AdminLessonDetailDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.lesson(lessonId),
    queryFn: ({ signal }) =>
      client.get<AdminLessonDetailDto>(`/api/learning/Lessons/${lessonId}/Admin`, { signal }),
    enabled: lessonId > 0,
  });
}
