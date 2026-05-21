/**
 * @learnexia/api-client — typed client to the .NET API + TanStack Query layer.
 *
 * Framework-agnostic: NO Expo/Next imports. The host app injects platform token
 * storage (via @learnexia/shared `TokenStorage`) and a sign-out callback.
 *
 *  - `client/`   fetch-based ApiClient: attaches JWT, unwraps `BaseResponse`,
 *                maps 422, single-flight 401 refresh.
 *  - `query/`    QueryClient factory, provider, query-key conventions.
 *  - `hooks/`    representative TanStack Query hooks (the pattern to follow).
 *  - `schemas`   typed DTO aliases over the generated Swagger contract.
 *
 * The envelope generics (`BaseResponse<T>`, `PaginatedResult<T>`) come from
 * @learnexia/shared and are re-exported here for convenience.
 */

// Envelope contract (re-exported from shared — single source of truth).
export type {
  BaseResponse,
  PaginatedResult,
  ValidationErrorResponse,
  UnwrappedResult,
} from '@learnexia/shared';

// HTTP client + typed errors.
export * from './client';

// TanStack Query layer.
export * from './query';

// Representative hooks + client context.
export * from './hooks';

// Generated DTO aliases.
export * from './schemas';
