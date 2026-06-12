/**
 * Named DTO aliases over the NSwag-generated client (`./generated`).
 *
 * `generated/nswag-client.ts` is GENERATED (`pnpm gen:api` → NSwag, from the
 * committed `swagger.json`) and committed — it is the typed contract: a `Client`
 * class with one method per endpoint, plus DTO interfaces and the
 * `*BaseResponse` envelope shapes. Do not hand-edit it (eslint ignores
 * `src/generated/**`). This hand-written adapter re-surfaces the DTO names the
 * hooks use, so callers import from `@learnexia/api-client` instead of reaching
 * into the generated module.
 *
 * NSwag standard (supersedes openapi-typescript): the previous
 * `components['schemas'][...]` indexing is gone — DTOs are now first-class named
 * interfaces emitted by NSwag, re-exported here directly. The generation flow is
 * documented in packages/api-client/README.md and docs/dev/FRONTEND_ARCHITECTURE.md.
 *
 * The response-envelope generics (`BaseResponse<T>` / `PaginatedResult<T>`) the
 * client unwraps against are owned by @learnexia/shared; the `*BaseResponse`
 * shapes here are the GENERATED concrete envelopes for each endpoint.
 */

export type {
  // P1-12-FE — Avatar upload/remove + new auth flows (FE-0b/0c/0d/0e/0f/0g)
  AvatarUploadResponse,
  AvatarUploadResponseBaseResponse,
  FileParameter,
  GoogleSignInCommand,
  ForgotPasswordCommand,
  ResetPasswordCommand,
  // W13 dashboard types (P2-09-FE)
  DashboardDto,
  DashboardDtoBaseResponse,
  ContinueTargetDto,
  // P4-06 renamed the dashboard mission sub-type DailyMissionDto → MissionSummary.
  MissionSummary,
  LeaguePreviewDto,
  // W12 quiz + lesson types
  SingleLessonResponse,
  SingleLessonResponseBaseResponse,
  StartAttemptResponse,
  StartAttemptResponseBaseResponse,
  SubmitAnswerCommand,
  SubmitAnswerResponse,
  SubmitAnswerResponseBaseResponse,
  AttemptSummaryDto,
  AttemptSummaryDtoBaseResponse,
  QuizQuestionDto,
  // Auth
  SignInCommand,
  RegisterParentCommand,
  RefreshTokenCommand,
  SignOutCommand,
  JwtAuthResponse,
  JwtAuthResponseBaseResponse,
  // Parent / family
  AddChildCommand,
  AddedChildResponse,
  AddedChildResponseBaseResponse,
  LinkChildCommand,
  LinkedChildResponse,
  LinkedChildResponseBaseResponse,
  LinkedChildResponseIEnumerableBaseResponse,
  // Current user
  MeResponse,
  MeResponseBaseResponse,
  // Account profile (P1-12 — profile read/update)
  AccountProfileResponse,
  AccountProfileResponseBaseResponse,
  UpdateMyProfileCommand,
  // Admin / user-management (consumed by legacy example hooks)
  UserProfileResponseDto,
  UserProfileResponseDtoBaseResponse,
  GetUserListResponse,
  GetUserListResponsePaginatedResult,
  // W10 / P2-12 — Notifications tab (interfaces/types)
  NotificationPreferenceItemDto,
  NotificationPreferencesResponse,
  NotificationPreferencesResponseBaseResponse,
  UpdateMyNotificationPreferencesCommand,
  // W10 / P2-12 — Linked children tab
  UpdateChildCommand,
  UpdatedChildResponse,
  UpdatedChildResponseBaseResponse,
  UnlinkChildCommand,
  BooleanBaseResponse,
  // W10 / P2-12 — Security tab
  ChangePasswordCommand,
  SessionInfo,
  SessionInfoListBaseResponse,
  // W10 / P2-12 — Plan & billing tab
  CurrentPlanResponse,
  CurrentPlanResponseBaseResponse,
  // P8-99-FE-2 — UI-language persistence (axis A)
  EditUserPreferredLanguageCommand,
  // P8-04-FE — child learning-language change (axis B)
  ChangeLearningLanguageCommand,
  ChangedLearningLanguageResponse,
  ChangedLearningLanguageResponseBaseResponse,
  // W11 / Learning (P2-02-FE + P2-03-FE)
  StudentSubjectDto,
  StudentSubjectDtoListBaseResponse,
  UnitWithLessonsDto,
  UnitWithLessonsDtoListBaseResponse,
  LessonInUnitDto,
  ConceptNodeDto,
  ConceptNodeDtoListBaseResponse,
  SkillNodeDto,
  MissingPrerequisiteDto,
  // P4-08 carryover (batch 2a) — gamification /Me responses + dashboard sub-DTOs
  MyBadgesResponse,
  MyBadgesResponseBaseResponse,
  BadgeStateDto,
  BadgeSummary,
  MyMissionsResponse,
  MyMissionsResponseBaseResponse,
  MissionStateDto,
  MyLeagueResponse,
  MyLeagueResponseBaseResponse,
  LeagueStandingDto,
  ActiveTimedEventDto,
  // P2-09 / A5 attempt-history (hook lands in batch 2d)
  AttemptListItemDto,
  AttemptListItemDtoListBaseResponse,
} from './generated';

export {
  // W11 / Learning enums
  NodeState,
  DifficultyLevel,
  // W12 quiz enum
  QuestionType,
} from './generated';

// P4-08 carryover (batch 2a) — gamification enums are VALUES (screens must
// reference enum members, never raw numbers/strings — CLAUDE.md i18n/enum rule).
export {
  BadgeRarity,
  BadgeRarityDto,
  MissionStatusDto,
  MissionTypeDto,
  MissionTargetTypeDto,
  LeagueTierDto,
} from './generated';

// NotificationCategory is an enum value (not just a type) — must be re-exported as a value.
export { NotificationCategory } from './generated';

// Re-export the generated typed client + its exception for advanced callers.
export {
  GeneratedApiClient,
  ApiException,
  type IClient,
} from './generated';
