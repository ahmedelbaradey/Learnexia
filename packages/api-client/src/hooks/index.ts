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

// --- P7-06 admin user-management read hooks (Batch A foundation) ---
// These hooks target GET /api/Admin/Users/* — a DISTINCT namespace from the
// legacy `useUserList`/`useUserProfile` hooks (which target /api/Users/…).
// P7-07/08 mutation hooks MUST invalidate `adminUsers.*` keys, never `users.*`.
export {
  useSearchUsers,
  type SearchUsersFilters,
  type AdminUserListItemDto,
} from './useSearchUsers';
export {
  useAdminUserProfile,
  type AdminUserProfileDto,
} from './useAdminUserProfile';
export {
  useUserFamily,
  type AdminFamilyDto,
  type AdminFamilyMemberDto,
} from './useUserFamily';
export {
  useUserActivity,
  type AdminActivitySummaryDto,
  type AdminXpSummary,
  type AdminStreakSummary,
  type AdminBadgesSummary,
  type AdminMissionsSummary,
  type AdminLeagueSummary,
} from './useUserActivity';

// --- P7-07 admin lifecycle mutation hooks (Batch C) ---
// Suspend / Reactivate / Delete — target POST|DELETE /api/Admin/Users/* (raw path).
// All three invalidate queryKeys.adminUsers.profile(id) + queryKeys.adminUsers.all.
// Output is string (the success message from BaseResponse<string>) — NOT a profile DTO.
// Refetch via invalidation; no optimistic updates.
export {
  useSuspendUser,
  type SuspendUserInput,
} from './useSuspendUser';
export {
  useReactivateUser,
  type ReactivateUserInput,
} from './useReactivateUser';
export {
  useDeleteUser,
  type DeleteUserInput,
} from './useDeleteUser';

// --- P7-08 admin child-edit mutation hooks (Batch D) ---
// Profile PATCH + Grade override POST + Learning-language POST (DESTRUCTIVE).
// All invalidate queryKeys.adminUsers.profile(childId) + queryKeys.adminUsers.all.
// Learning-language additionally invalidates queryKeys.adminUsers.activity(childId).
// NOTE: useAdminChangeChildLearningLanguage is DISTINCT from useChangeLearningLanguage
// (the parent-app P8-04 hook targeting a different endpoint with different field names).
export {
  useUpdateChildProfile,
  type UpdateChildProfileInput,
} from './useUpdateChildProfile';
export {
  useOverrideChildGrade,
  type OverrideChildGradeInput,
  type ChildGradeOverrideResponseDto,
} from './useOverrideChildGrade';
export {
  useAdminChangeChildLearningLanguage,
  type AdminChangeChildLearningLanguageInput,
  type AdminChangedLearningLanguageResponseDto,
} from './useAdminChangeChildLearningLanguage';

// --- P7-01 admin curriculum hooks (Batch 2a-A — foundation) ---
// Subjects + Units CRUD + Reorder + SetActive + Coverage + Grades.
// All query hooks use adminCurriculum.* keys (DISTINCT from learning.* student hooks).
// All mutation hooks return BaseResponse<string> (message) — refetch via invalidation.
export type {
  SubjectDto,
  UnitDto,
  SubjectLanguageCoverageDto,
  SubjectLanguageCoverageReportDto,
  GradeDto,
  AddSubjectDto,
  EditSubjectDto,
  AddUnitDto,
  EditUnitDto,
  LessonDto,
  AdminContentBlockDto,
  AdminQuestionDto,
  // SkillDto and SkillGraphDto are exported from the P7-03 block below
} from './curriculum.types';

export {
  useSubjectList,
  type SubjectListFilters,
} from './useSubjectList';
export {
  useUnitList,
  type UnitListFilters,
} from './useUnitList';
export { useSubjectCoverage } from './useSubjectCoverage';
export { useAdminGrades } from './useAdminGrades';

export { useCreateSubject } from './useCreateSubject';
export { useUpdateSubject } from './useUpdateSubject';
export {
  useReorderSubjects,
  type ReorderSubjectsInput,
} from './useReorderSubjects';
export {
  useSetSubjectActive,
  type SetSubjectActiveInput,
} from './useSetSubjectActive';
export {
  useDeleteSubject,
  type DeleteSubjectInput,
} from './useDeleteSubject';

export { useCreateUnit } from './useCreateUnit';
export { useUpdateUnit } from './useUpdateUnit';
export {
  useReorderUnits,
  type ReorderUnitsInput,
} from './useReorderUnits';
export {
  useSetUnitActive,
  type SetUnitActiveInput,
} from './useSetUnitActive';
export {
  useDeleteUnit,
  type DeleteUnitInput,
} from './useDeleteUnit';

// --- P7-02 admin curriculum lesson + content-block hooks (Batch 2b-A) ---
// Lessons CRUD + Reorder + SetActive; ContentBlocks Add/Edit/Delete/Reorder.
// All target /api/learning/Lessons/* and /api/learning/ContentBlocks/* (AdminOnly).
// All mutation hooks return BaseResponse<string> — refetch via adminCurriculum.*
// invalidation; no optimistic update except reorder (local-state-first then save).
export type {
  AdminSingleLessonResponse,
  AdminLessonDetailDto,
  AddLessonDto,
  EditLessonDto,
  AddContentBlockDto,
  EditContentBlockDto,
  TextPayload,
  ImagePayload,
  VideoPayload,
  CalloutPayload,
  ParsedPayload,
} from './curriculum.types';

export {
  useLessonsByUnit,
  type LessonListFilters,
} from './useLessonsByUnit';
export { useAdminLesson } from './useAdminLesson';
export { useCreateLesson } from './useCreateLesson';
export { useEditLesson } from './useEditLesson';
export {
  useDeleteLesson,
  type DeleteLessonInput,
} from './useDeleteLesson';
export {
  useSetLessonActive,
  type SetLessonActiveInput,
} from './useSetLessonActive';
export {
  useReorderLessons,
  type ReorderLessonsInput,
} from './useReorderLessons';

export { useAddContentBlock } from './useAddContentBlock';
export {
  useEditContentBlock,
  type EditContentBlockInput,
} from './useEditContentBlock';
export {
  useDeleteContentBlock,
  type DeleteContentBlockInput,
} from './useDeleteContentBlock';
export {
  useReorderContentBlocks,
  type ReorderContentBlocksInput,
} from './useReorderContentBlocks';

// --- P7-04 admin curriculum question hooks (Batch 2b-E) ---
// Questions CRUD + Reorder + SetActive for per-lesson question manager.
// All target /api/Learning/Questions/* (AdminOnly).
// All mutation hooks return BaseResponse<string> (message) — refetch via
// adminCurriculum.questions(lessonId) invalidation; no optimistic updates.
// CorrectAnswer contract: non-Matching = raw scalar on wire (no JSON.parse/stringify);
// Matching = JSON object strings (FE is the authoritative serializer).
export type {
  AddQuestionPayload,
  EditQuestionPayload,
} from './curriculum.types';

export { useAdminQuestionsByLesson } from './useAdminQuestionsByLesson';
export { useAdminQuestion } from './useAdminQuestion';
export { useAddQuestion } from './useAddQuestion';
export { useEditQuestion, type EditQuestionInput } from './useEditQuestion';
export { useDeleteQuestion, type DeleteQuestionInput } from './useDeleteQuestion';
export { useReorderQuestions, type ReorderQuestionsInput } from './useReorderQuestions';
export { useSetQuestionActive, type SetQuestionActiveInput } from './useSetQuestionActive';

// --- P7-03 admin curriculum skill + graph hooks (Batch 2c-A) ---
// Skills CRUD (AdminOnly) + knowledge graph read/edge mutations (node-id space).
// All skill mutations return BaseResponse<string> — refetch via adminCurriculum.*
// invalidation; no optimistic updates (plan D8).
// Edge mutations (Add/Remove) also invalidate prerequisites/unlockedBy keys.
// Concept list uses standalone ['concepts','list'] key (anonymous endpoint).
// Gap 1 RESOLVED: ConceptListItemDto.subjectId IS present on the wire.
// Gap 2 RESOLVED: SingleSkillResponse.isActive IS present (AdminOnly endpoint).
export type {
  SingleSkillResponse,
  AddSkillDto,
  EditSkillDto,
  KnowledgeNodeDto,
  KnowledgeEdgeDto,
  SkillGraphDto,
  StudentKnowledgeNodeDto,
  AddKnowledgeEdgeDto,
  ConceptListItemDto,
} from './curriculum.types';

export {
  useSkillList,
  type SkillListFilters,
} from './useSkillList';
export {
  useCreateSkill,
  type CreateSkillInput,
} from './useCreateSkill';
export {
  useUpdateSkill,
  type UpdateSkillInput,
} from './useUpdateSkill';
export {
  useDeleteSkill,
  type DeleteSkillInput,
} from './useDeleteSkill';
export { useSkillGraph } from './useSkillGraph';
export {
  useAddKnowledgeEdge,
  type AddKnowledgeEdgeInput,
} from './useAddKnowledgeEdge';
export {
  useRemoveKnowledgeEdge,
  type RemoveKnowledgeEdgeInput,
} from './useRemoveKnowledgeEdge';
export { useConceptList } from './useConceptList';

// --- P7-05 curriculum lifecycle hooks (Batch 2a-C — lifecycle backbone) ---
// Publish/Unpublish/Archive/Restore + Rollback + VersionHistory + Preview +
// PublicationCoverage. All target /api/learning/ContentLifecycle/* (AdminOnly).
// All mutation hooks return BaseResponse<string> (message) — refetch via
// adminCurriculum.* key invalidation; no optimistic updates.
// NOTE: useRollbackToVersion's `_feOnlyReason` is a FE-only UX guardrail;
// the reason is NOT sent to the backend (DG-4).
export {
  useContentVersionHistory,
  usePreviewContent,
  usePublicationCoverage,
  useTransitionLifecycle,
  useRollbackToVersion,
  type ContentVersionDto,
  type PreviewDto,
  type PublicationCoverageSlotDto,
  type PublicationCoverageReportDto,
  type TransitionLifecycleInput,
  type RollbackVersionInput,
} from './useLifecycle';
