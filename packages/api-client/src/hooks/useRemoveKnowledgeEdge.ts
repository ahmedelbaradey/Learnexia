/**
 * useRemoveKnowledgeEdge — DELETE /api/Learning/KnowledgeGraph/Edges/{edgeId} (AdminOnly).
 *
 * Removes a prerequisite edge by its KnowledgeEdge.Id.
 * Returns BaseResponse<string> (a success message).
 *
 * On success: invalidate graph(subjectId) + prerequisites(nodeId) + unlockedBy(sourceNodeId).
 * No optimistic updates (plan D8).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';

export interface RemoveKnowledgeEdgeInput {
  edgeId: number;
  /** Used to invalidate the graph query on success. */
  subjectId: number;
  /** The target node (edge.targetNodeId) — invalidates prerequisites list. */
  targetNodeId: number;
  /** The source node (edge.sourceNodeId) — invalidates unlocked-by list. */
  sourceNodeId: number;
}

export function useRemoveKnowledgeEdge(): UseMutationResult<string, Error, RemoveKnowledgeEdgeInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, RemoveKnowledgeEdgeInput>({
    mutationFn: ({ edgeId }) =>
      client.delete<string>(`/api/Learning/KnowledgeGraph/Edges/${edgeId}`),
    onSuccess: async (_data, { subjectId, targetNodeId, sourceNodeId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.graph(subjectId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.prerequisites(targetNodeId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.unlockedBy(sourceNodeId) }),
      ]);
    },
  });
}
