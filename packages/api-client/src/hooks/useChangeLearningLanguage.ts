/**
 * useChangeLearningLanguage — PUT /api/Parent/Change-Learning-Language (authenticated).
 *
 * Wraps the NSwag `changeLearningLanguage` method and unwraps to the typed
 * `ChangedLearningLanguageResponse`. The acting parent is resolved server-side
 * from the JWT; `childId` in the body must be one of their linked children
 * (foreign child → 403 from the BE).
 *
 * Body: `{ childId, newLearningLanguage, confirmFreshStart }`.
 * NOTE: the field is `newLearningLanguage` (NOT `learningLanguage`) — per the
 * generated contract and the P8-04-FE design spec.
 *
 * Behaviour:
 * - Missing or false `confirmFreshStart` → BE returns 424 (BusinessValidation).
 *   The UI gates this at the checkbox; the hook maps it defensively via byStatus.
 * - 403 → not your child / caller is not a parent.
 * - Same language → BE no-op success (treated as success by this hook).
 *
 * On success: invalidates `queryKeys.family.myChildren()` so the per-child
 * "Learning language" row reflects the newly persisted language.
 *
 * No server data in Zustand. This hook only handles server-side persistence;
 * no client store update is needed (the row reads from useMyChildren).
 */
import {
  useMutation,
  useQueryClient,
  type UseMutationResult,
} from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { ChangedLearningLanguageResponse, ChangeLearningLanguageCommand } from '../schemas';

export function useChangeLearningLanguage(): UseMutationResult<
  ChangedLearningLanguageResponse | null | undefined,
  Error,
  ChangeLearningLanguageCommand
> {
  const client = useTypedClient();
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: (
      input: ChangeLearningLanguageCommand,
    ): Promise<ChangedLearningLanguageResponse | null | undefined> =>
      unwrapEnvelope(
        client.changeLearningLanguage(input),
      ) as Promise<ChangedLearningLanguageResponse | null | undefined>,
    onSuccess: async () => {
      await queryClient.invalidateQueries({
        queryKey: queryKeys.family.myChildren(),
      });
    },
  });
}
