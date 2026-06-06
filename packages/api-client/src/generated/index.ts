/**
 * Barrel for the NSwag-generated TypeScript client.
 *
 * `nswag-client.ts` is GENERATED (`pnpm gen:api` → NSwag, from the committed
 * `swagger.json` snapshot) and committed — it is the typed contract: one
 * `Client` class with a typed method per endpoint, plus DTO interfaces and the
 * `*BaseResponse` envelope shapes. Do NOT hand-edit it (the eslint config
 * ignores `src/generated/**`).
 *
 * Generation flow (see packages/api-client/README.md):
 *   1. `refresh:swagger` — pull the OpenAPI snapshot from the running API.
 *   2. `gen:api`         — NSwag → this client + DTOs.
 *   3. hooks wrap the typed client through the existing `ApiClient` transport.
 */

export {
  Client as GeneratedApiClient,
  ApiException,
  type IClient,
} from './nswag-client';

export type {
  // P1-12-FE — Avatar upload/remove + new auth flows
  AvatarUploadResponse,
  AvatarUploadResponseBaseResponse,
  FileParameter,
  GoogleSignInCommand,
  ForgotPasswordCommand,
  ResetPasswordCommand,
  // Existing DTOs
  SignInCommand,
  RegisterParentCommand,
  RefreshTokenCommand,
  AccessTokenQuery,
  SignOutCommand,
  AddChildCommand,
  AddedChildResponse,
  AddedChildResponseBaseResponse,
  LinkChildCommand,
  LinkedChildResponse,
  LinkedChildResponseBaseResponse,
  LinkedChildResponseIEnumerableBaseResponse,
  MeResponse,
  MeResponseBaseResponse,
  AccountProfileResponse,
  AccountProfileResponseBaseResponse,
  UpdateMyProfileCommand,
  JwtAuthResponse,
  JwtAuthResponseBaseResponse,
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
  // W11 — learning types (P2-02-FE + P2-03-FE)
  StudentSubjectDto,
  StudentSubjectDtoListBaseResponse,
  UnitWithLessonsDto,
  UnitWithLessonsDtoListBaseResponse,
  LessonInUnitDto,
  ConceptNodeDto,
  ConceptNodeDtoListBaseResponse,
  SkillNodeDto,
  MissingPrerequisiteDto,
  // W12 — quiz + lesson types (P2-05-FE + P2-06-FE + P2-07-FE)
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
  // W13 — dashboard types (P2-09-FE)
  DashboardDto,
  DashboardDtoBaseResponse,
  ContinueTargetDto,
  // P4-06 renamed the dashboard mission sub-type DailyMissionDto → MissionSummary
  // (DashboardDto now exposes dailyMissions: MissionSummary[] + weeklyMission: MissionSummary).
  MissionSummary,
  LeaguePreviewDto,
} from './nswag-client';

export {
  // W11 — learning enums
  NodeState,
  DifficultyLevel,
  // W12 — quiz enum
  QuestionType,
} from './nswag-client';

// P2-12 — NotificationCategory is an enum (not a type); must be exported as a value.
export { NotificationCategory } from './nswag-client';
