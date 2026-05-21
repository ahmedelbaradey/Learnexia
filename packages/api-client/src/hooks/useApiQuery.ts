/**
 * useApiQuery<T> — generic TanStack Query wrapper.
 *
 * Downstream feature hooks use this instead of reaching directly into
 * `useQuery`. The `fetcher` receives the configured `ApiClient` (from context)
 * and returns the already-unwrapped data: `ApiClient.get/post/…` calls
 * `handleResponse` internally, so the envelope is stripped before the value
 * reaches this hook. Do NOT re-unwrap here.
 *
 * Retry policy inherits from the `QueryClient` created by `createQueryClient`:
 * `ApiAuthError` and `ValidationError` are never retried — this hook must not
 * override that behaviour via its own `retry` option.
 *
 * Usage:
 *   function useSubjectList(gradeId: number) {
 *     return useApiQuery<SubjectDto[]>(
 *       ['subjects', 'list', gradeId],
 *       (client) => client.get(`/api/Catalog/Subjects?gradeId=${gradeId}`),
 *     );
 *   }
 */

import {
  useQuery,
  type QueryKey,
  type UseQueryOptions,
  type UseQueryResult,
} from '@tanstack/react-query';

import type { ApiClient } from '../client/apiClient';
import { useApiClient } from './apiClientContext';

/**
 * Options forwarded to TanStack Query's `useQuery` minus the fields that
 * `useApiQuery` owns (`queryKey`, `queryFn`). `retry` is intentionally kept
 * so callers can tighten (never loosen) the default policy if needed for an
 * edge case.
 */
export type UseApiQueryOptions<T> = Omit<
  UseQueryOptions<T, Error, T, QueryKey>,
  'queryKey' | 'queryFn'
>;

/**
 * A fetcher function that receives the `ApiClient` instance and an
 * `AbortSignal` (wired from TanStack Query's `queryFn` context) and returns
 * a promise of the already-unwrapped response data.
 */
export type ApiQueryFetcher<T> = (
  client: ApiClient,
  signal: AbortSignal,
) => Promise<T>;

/**
 * Thin generic wrapper over `useQuery`.
 *
 * @param key     Stable query key — follow the `queryKeys` tuple conventions
 *                (`[domain, scope, ...params]`). Inline arrays are fine for
 *                feature-local queries; add to `queryKeys.ts` when the key
 *                needs to be invalidated from multiple places.
 * @param fetcher Receives the `ApiClient` and AbortSignal; returns unwrapped data.
 * @param options Additional `UseQueryOptions` (staleTime, enabled, select, …).
 *                Do not pass `retry` unless you are explicitly narrowing it.
 */
export function useApiQuery<T>(
  key: QueryKey,
  fetcher: ApiQueryFetcher<T>,
  options?: UseApiQueryOptions<T>,
): UseQueryResult<T, Error> {
  const client = useApiClient();

  return useQuery<T, Error, T, QueryKey>({
    ...options,
    queryKey: key,
    queryFn: ({ signal }) => fetcher(client, signal),
  });
}
