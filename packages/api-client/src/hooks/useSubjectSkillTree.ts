/**
 * useSubjectSkillTree — GET /api/learning/Subjects/{id}/SkillTree
 *
 * Auth-required: 401 on unauthenticated. The existing api-client interceptor
 * handles refresh; if refresh fails the auth-route guard bounces to login.
 * Disabled when subjectId is 0 or negative (invalid / not yet resolved).
 *
 * Mirror of useMe pattern: thin TanStack Query wrapper → unwrapEnvelope.
 */

import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { ConceptNodeDto } from '../schemas';

export function useSubjectSkillTree(
  subjectId: number,
  options?: UseApiQueryOptions<ConceptNodeDto[]>,
): UseQueryResult<ConceptNodeDto[], Error> {
  const client = useTypedClient();

  return useQuery<ConceptNodeDto[], Error, ConceptNodeDto[]>({
    ...options,
    queryKey: queryKeys.learning.subjectSkillTree(subjectId),
    enabled: subjectId > 0 && (options?.enabled !== false),
    queryFn: () =>
      unwrapEnvelope(client.skillTree(subjectId)).then(
        (data) => data ?? [],
      ) as Promise<ConceptNodeDto[]>,
  });
}
