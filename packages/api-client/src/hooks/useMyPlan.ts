/**
 * useMyPlan — GET /api/Users/Account/Plan (authenticated).
 *
 * Wraps the NSwag `plan` method and unwraps to `CurrentPlanResponse`
 * (`{ planName, status }`). In W10 the BE returns a stub: Free / Active.
 *
 * The Manage-subscription CTA is disabled and marked TODO(P2-12-PAYMENTS)
 * — no payment integration exists yet.
 */
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

import { useTypedClient } from './apiClientContext';
import { unwrapEnvelope } from '../client/typedClient';
import { queryKeys } from '../query/queryKeys';
import type { UseApiQueryOptions } from './useApiQuery';
import type { CurrentPlanResponse } from '../schemas';

export function useMyPlan(
  options?: UseApiQueryOptions<CurrentPlanResponse>,
): UseQueryResult<CurrentPlanResponse, Error> {
  const client = useTypedClient();
  return useQuery<CurrentPlanResponse, Error, CurrentPlanResponse>({
    ...options,
    queryKey: queryKeys.account.plan(),
    queryFn: () =>
      unwrapEnvelope(client.plan()) as Promise<CurrentPlanResponse>,
  });
}
