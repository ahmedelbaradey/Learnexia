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

// P1-12-FE Batch 1 — avatar + new auth flows (FE-0b/0c/0d/0e/0f).
// useUploadAvatar + useRemoveAvatar are consumed in this batch (avatar UI, FE-2).
// useGoogleSignIn, useForgotPassword, useResetPassword are wired but consumed in
// later batches (Batch 4: Google OAuth, Batch 3: forgot/reset screens).
export { useUploadAvatar } from './useUploadAvatar';
export { useRemoveAvatar } from './useRemoveAvatar';
export { useGoogleSignIn } from './useGoogleSignIn';
export { useForgotPassword } from './useForgotPassword';
export { useResetPassword } from './useResetPassword';

// W10 / P2-12 settings-tab hooks.
export { useNotificationPreferences } from './useNotificationPreferences';
export { useUpdateNotificationPreferences } from './useUpdateNotificationPreferences';
export { useUpdateChild } from './useUpdateChild';
export { useUnlinkChild } from './useUnlinkChild';
export { useChangePassword } from './useChangePassword';
export { useMySessions } from './useMySessions';
export { useSignOutOtherSessions } from './useSignOutOtherSessions';
export { useMyPlan } from './useMyPlan';

// P8-99-FE — UI-language persistence (axis A).
export { useUpdateUserLanguage } from './useUpdateUserLanguage';

// P8-04-FE — child learning-language change (axis B, parent-only).
export { useChangeLearningLanguage } from './useChangeLearningLanguage';

// W11 learning hooks (P2-02-FE + P2-03-FE).
export { useSubjectsForGrade } from './useSubjectsForGrade';
export { useSubjectLessons } from './useSubjectLessons';
export { useSubjectSkillTree } from './useSubjectSkillTree';

// --- W13 dashboard hook (P2-09-FE) ---
export { useDashboard } from './useDashboard';

// --- A5 attempt history (CO-FE-7, carryover batch 2c/2d) ---
export { useStudentAttempts, ATTEMPT_STATUS } from './useStudentAttempts';
export type { AttemptStatusValue } from './useStudentAttempts';

// --- W12 quiz hooks (P2-05-FE + P2-06-FE + P2-07-FE) ---
export { useLesson } from './useLesson';
export { useStartAttempt } from './useStartAttempt';
export type { StartAttemptInput } from './useStartAttempt';
export { useSubmitAnswer } from './useSubmitAnswer';
export { useCompleteAttempt } from './useCompleteAttempt';
export type { CompleteAttemptInput } from './useCompleteAttempt';
export { useAbandonAttempt } from './useAbandonAttempt';
export type { AbandonAttemptInput } from './useAbandonAttempt';

// --- P4-08 carryover (batch 2a) — gamification /Me hooks ---
export { useMyBadges } from './useMyBadges';
export { useMyMissions } from './useMyMissions';
export { useMyLeague } from './useMyLeague';
