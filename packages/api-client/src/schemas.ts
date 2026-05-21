/**
 * Convenience re-exports over the openapi-typescript output
 * (`./generated/api-types.ts`).
 *
 * `generated/api-types.ts` is GENERATED (`pnpm gen:api`) and committed — it is
 * the typed contract. Do not hand-edit it (the eslint config ignores
 * `src/generated/**`). This hand-written adapter surfaces the schema/operation
 * objects and a few named DTO aliases the hooks use, so callers import from
 * `@learnexia/api-client` instead of indexing `components['schemas']`.
 *
 * The response envelopes here (`*BaseResponse`, `*PaginatedResult`) are the
 * GENERATED shapes; @learnexia/shared owns the hand-written generic
 * `BaseResponse<T>` / `PaginatedResult<T>` that the client unwraps against.
 */

import type { components, paths, operations } from './generated/api-types';

export type { components, paths, operations } from './generated/api-types';

/** All generated DTO schemas, keyed by Swagger schema name. */
export type Schemas = components['schemas'];

// --- Named DTO aliases used by the representative hooks ------------------

export type SignInCommand = Schemas['SignInCommand'];
export type RefreshTokenCommand = Schemas['RefreshTokenCommand'];
export type JwtAuthResponse = Schemas['JwtAuthResponse'];
export type JwtAuthResponseBaseResponse = Schemas['JwtAuthResponseBaseResponse'];

export type UserProfileResponseDto = Schemas['UserProfileResponseDto'];
export type UserProfileResponseDtoBaseResponse =
  Schemas['UserProfileResponseDtoBaseResponse'];

export type GetUserListResponse = Schemas['GetUserListResponse'];
export type GetUserListResponsePaginatedResult =
  Schemas['GetUserListResponsePaginatedResult'];

export type { paths as ApiPaths, operations as ApiOperations };
