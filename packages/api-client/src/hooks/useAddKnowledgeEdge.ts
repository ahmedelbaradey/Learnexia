/**
 * useAddKnowledgeEdge — POST /api/Learning/KnowledgeGraph/Edges (AdminOnly).
 *
 * Creates a prerequisite edge between two KnowledgeNodes (by node id, NOT skill id).
 * Returns BaseResponse<string> (a success message or rejection reason).
 *
 * Rejection cases (thrown as ApiEnvelopeError, status 400, Successed=false):
 *   - Cycle detected     → KnowledgeEdgeWouldCreateCycle
 *   - Cross-language     → KnowledgeEdgeCrossLanguageForbidden
 *   - Duplicate edge     → KnowledgeEdgeDuplicate
 *   - Node not found     → KnowledgeNodeNotFound
 *   - Strength out of range → KnowledgeEdgeStrengthOutOfRange
 *
 * On success: invalidate graph(subjectId) + prerequisites(nodeId) + unlockedBy(nodeId).
 * No optimistic updates (plan D8, brief contract).
 *
 * v1 hard-codes relationshipType=Prerequisite (0) and strength=1.0 (plan D-OQ6).
 */

import { useMutation, useQueryClient, type UseMutationResult } from '@tanstack/react-query';
import { useApiClient } from './apiClientContext';
import { queryKeys } from '../query/queryKeys';
import type { AddKnowledgeEdgeDto } from './curriculum.types';

export interface AddKnowledgeEdgeInput extends AddKnowledgeEdgeDto {
  /** The subject tree id — used to invalidate the graph query on success. */
  _subjectId: number;
  /** The target node id — used to invalidate prerequisites/unlockedBy. */
  _targetNodeId: number;
}

export function useAddKnowledgeEdge(): UseMutationResult<string, Error, AddKnowledgeEdgeInput> {
  const client = useApiClient();
  const queryClient = useQueryClient();

  return useMutation<string, Error, AddKnowledgeEdgeInput>({
    mutationFn: ({ _subjectId: _unused, _targetNodeId: _unused2, ...body }) =>
      client.post<string>('/api/Learning/KnowledgeGraph/Edges', { body }),
    onSuccess: async (_data, { _subjectId, _targetNodeId, sourceNodeId }) => {
      await Promise.all([
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.graph(_subjectId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.prerequisites(_targetNodeId) }),
        queryClient.invalidateQueries({ queryKey: queryKeys.adminCurriculum.unlockedBy(sourceNodeId) }),
      ]);
    },
  });
}
