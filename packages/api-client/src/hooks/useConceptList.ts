/**
 * useConceptList — GET /api/learning/Concepts/List (anonymous endpoint).
 *
 * Fetches all concepts for the concept picker in SkillForm.
 * The endpoint is anonymous (no [Authorize]) per the merged ConceptsController.
 * The backend ListConceptsQuery accepts an optional SubjectId filter — use it
 * when a subject is selected to scope the picker to that subject's concepts.
 *
 * Gap 1 RESOLVED: ConceptDto.SubjectId (int, non-nullable) IS returned on the wire.
 * The FE can additionally filter client-side by concept.subjectId if the server
 * param is not available in a given request context.
 *
 * Backend contract (verified against ListConceptsQueryHandler + ConceptsController):
 *   GET /api/learning/Concepts/List
 *   Params (PascalCase): SubjectId?, PageNumber?, PageSize?, OrderBy?
 *   Returns: BaseResponse<PaginatedResult<SingleConceptResponse>>
 *   Auth: anonymous (no [Authorize])
 *
 * Uses a standalone query key ['concepts', 'list'] — separate from adminCurriculum.*
 * because concepts are shared/read-only content, not admin-mutations.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';
import type { PaginatedResult } from '@learnexia/shared';
import { useApiClient } from './apiClientContext';
import type { ConceptListItemDto } from './curriculum.types';

const CONCEPT_LIST_KEY = ['concepts', 'list'] as const;
const CONCEPTS_PAGE_SIZE = 200; // request all (concepts per subject are small)

export function useConceptList(
  subjectId?: number,
): UseQueryResult<PaginatedResult<ConceptListItemDto>, Error> {
  const client = useApiClient();

  return useQuery({
    queryKey: [...CONCEPT_LIST_KEY, { subjectId }],
    queryFn: ({ signal }) =>
      client.getPaginated<ConceptListItemDto>('/api/learning/Concepts/List', {
        query: {
          SubjectId: subjectId,
          PageNumber: 1,
          PageSize: CONCEPTS_PAGE_SIZE,
        },
        signal,
      }),
    staleTime: 5 * 60 * 1000, // concepts don't change often; 5 min stale
  });
}
