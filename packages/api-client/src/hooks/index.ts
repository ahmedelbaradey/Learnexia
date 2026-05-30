export {
  ApiClientProvider,
  useApiClient,
  useTypedClient,
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

// P1-09 auth + family hooks.
export { useRegisterParent } from './useRegisterParent';
export { useSignOut } from './useSignOut';
export { useAddChild } from './useAddChild';
export { useLinkChild } from './useLinkChild';
export { useMe } from './useMe';
export { useMyChildren } from './useMyChildren';

// P1-12 account-profile hooks.
export { useMyProfile } from './useMyProfile';
export { useUpdateProfile } from './useUpdateProfile';

// W11 learning hooks (P2-02-FE + P2-03-FE).
export { useSubjectsForGrade } from './useSubjectsForGrade';
export { useSubjectLessons } from './useSubjectLessons';
export { useSubjectSkillTree } from './useSubjectSkillTree';
