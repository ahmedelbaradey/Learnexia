/**
 * useSkillGraph — GET /api/Learning/KnowledgeGraph/Graph?subjectId={id} (AdminOnly).
 *
 * Fetches the full knowledge graph for a subject tree (all nodes + all edges).
 * One subject = one (SubjectCode, Language) tree; language scope is implicit.
 * No `language` query param — the backend scopes by subjectId.
 *
 * Backend contract (P7-03, verified against KnowledgeGraphController.cs):
 *   GET /api/Learning/KnowledgeGraph/Graph?subjectId={subjectId}
 *   Returns: BaseResponse<SkillGraphDto> = { subjectId, nodes[], edges[] }
 *   Auth: AdminOnly
 *
 * Enabled only when subjectId > 0 (no fetch on initial page load).
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { SkillGraphDto } from './curriculum.types';

export function useSkillGraph(
  subjectId: number,
): UseQueryResult<SkillGraphDto, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: queryKeys.adminCurriculum.graph(subjectId),
    enabled: subjectId > 0,
    queryFn: ({ signal }) =>
      client.get<SkillGraphDto>('/api/Learning/KnowledgeGraph/Graph', {
        query: { subjectId },
        signal,
      }),
  });
}
