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
  // Auth
  SignInCommand,
  RegisterParentCommand,
  RefreshTokenCommand,
  SignOutCommand,
  JwtAuthResponse,
  JwtAuthResponseBaseResponse,
  // Social / password reset (P1-12)
  GoogleSignInCommand,
  ForgotPasswordCommand,
  ResetPasswordCommand,
  // Parent / family
  AddChildCommand,
  AddedChildResponse,
  AddedChildResponseBaseResponse,
  LinkChildCommand,
  LinkedChildResponse,
  LinkedChildResponseBaseResponse,
  LinkedChildResponseIEnumerableBaseResponse,
  UpdateChildCommand,
  UpdatedChildResponse,
  UpdatedChildResponseBaseResponse,
  UnlinkChildCommand,
  // Current user
  MeResponse,
  MeResponseBaseResponse,
  // Account profile (P1-12 — profile read/update + avatar)
  AccountProfileResponse,
  AccountProfileResponseBaseResponse,
  UpdateMyProfileCommand,
  AvatarUploadResponse,
  AvatarUploadResponseBaseResponse,
  FileParameter,
  // Admin / user-management (consumed by legacy example hooks)
  UserProfileResponseDto,
  UserProfileResponseDtoBaseResponse,
  GetUserListResponse,
  GetUserListResponsePaginatedResult,
} from './generated';

// Re-export the generated typed client + its exception for advanced callers.
export {
  GeneratedApiClient,
  ApiException,
  type IClient,
} from './generated';
