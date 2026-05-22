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
  JwtAuthResponse,
  JwtAuthResponseBaseResponse,
  UserProfileResponseDto,
  UserProfileResponseDtoBaseResponse,
  GetUserListResponse,
  GetUserListResponsePaginatedResult,
} from './nswag-client';
