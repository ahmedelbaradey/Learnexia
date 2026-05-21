export {
  ApiClientProvider,
  useApiClient,
  type ApiClientProviderProps,
} from './apiClientContext';

// Generic hooks — the primary reusable API for all feature hooks.
export {
  useApiQuery,
  type ApiQueryFetcher,
  type UseApiQueryOptions,
} from './useApiQuery';
export {
  useApiMutation,
  type ApiMutationFn,
  type UseApiMutationOptions,
} from './useApiMutation';

// Concrete example hooks (follow these patterns when building feature hooks).
export { useSignIn } from './useSignIn';
export { useUserProfile, useCurrentUser } from './useUserProfile';
export { useUserList, type UserListFilters } from './useUserList';
