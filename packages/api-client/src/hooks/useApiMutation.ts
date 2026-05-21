/**
 * useApiMutation<TInput, TOutput> — generic TanStack Query mutation wrapper.
 *
 * Downstream feature hooks use this instead of reaching directly into
 * `useMutation`. The `mutationFn` receives the configured `ApiClient` (from
 * context) and the caller-supplied input, and returns the already-unwrapped
 * output: `ApiClient.post/put/delete/…` strips the `BaseResponse` envelope
 * internally. Do NOT re-unwrap here.
 *
 * Retry is always disabled for mutations at the `QueryClient` level
 * (`createQueryClient` sets `mutations.retry: false`). This hook does not
 * override that behaviour.
 *
 * Usage:
 *   function useEnrollStudent() {
 *     return useApiMutation<EnrollInput, EnrollmentDto>(
 *       (client, input) =>
 *         client.post('/api/Learning/Enrollments', { body: input }),
 *     );
 *   }
 */

import {
  useMutation,
  type UseMutationOptions,
  type UseMutationResult,
} from '@tanstack/react-query';

import type { ApiClient } from '../client/apiClient';
import { useApiClient } from './apiClientContext';

/**
 * Options forwarded to TanStack Query's `useMutation` minus the `mutationFn`
 * field which `useApiMutation` constructs internally.
 */
export type UseApiMutationOptions<TInput, TOutput> = Omit<
  UseMutationOptions<TOutput, Error, TInput>,
  'mutationFn'
>;

/**
 * A mutation function that receives the `ApiClient` instance and the caller-
 * supplied input, and returns a promise of the already-unwrapped output.
 */
export type ApiMutationFn<TInput, TOutput> = (
  client: ApiClient,
  input: TInput,
) => Promise<TOutput>;

/**
 * Thin generic wrapper over `useMutation`.
 *
 * @param mutationFn Receives the `ApiClient` and caller input; returns unwrapped data.
 * @param options    Additional `UseMutationOptions` (onSuccess, onError, …).
 *                   Do not pass `retry` — mutations never retry per global policy.
 */
export function useApiMutation<TInput, TOutput = void>(
  mutationFn: ApiMutationFn<TInput, TOutput>,
  options?: UseApiMutationOptions<TInput, TOutput>,
): UseMutationResult<TOutput, Error, TInput> {
  const client = useApiClient();

  return useMutation<TOutput, Error, TInput>({
    ...options,
    mutationFn: (input: TInput) => mutationFn(client, input),
  });
}
