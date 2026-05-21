/**
 * API envelope types — the single source of truth for the backend response contract.
 *
 * Shapes are cross-checked against `packages/api-client/swagger.json`
 * (OpenAPI v3, `components.schemas.*BaseResponse` / `*PaginatedResult`).
 *
 * NOTE: the success flag is spelled `successed` (sic) to match the backend
 * (`BaseResponse.Successed` in .NET). Do NOT rename it to `succeeded`.
 *
 * `@learnexia/api-client` imports these types so the whole frontend agrees on
 * one envelope contract.
 */

/**
 * Backend `BaseResponse<T>` envelope.
 *
 * swagger: `JwtAuthResponseBaseResponse`, `StringBaseResponse`, etc. all share
 * `{ statusCode, successed, message?, data, errors? }`.
 */
export interface BaseResponse<T> {
  /** HTTP status code echoed in the body (swagger: `HttpStatusCode` enum). */
  statusCode: number;
  /** Success flag — backend spelling is `successed` (sic). */
  successed: boolean;
  /** Optional human-readable message (nullable on the wire). */
  message?: string | null;
  /** The payload. */
  data: T;
  /** Error detail entries; shape is open per the backend (array of objects/strings). */
  errors?: unknown[] | null;
}

/**
 * Backend `PaginatedResult<T>` — a `BaseResponse` whose `data` is a list,
 * augmented with paging metadata.
 *
 * swagger: `GetUserListResponsePaginatedResult` adds
 * `currentPage`, `totalCount`, `totalPages`, `pageSize` (+ read-only
 * `hasPreviousPage` / `hasNextPage`).
 */
export interface PaginatedResult<T> extends BaseResponse<T[]> {
  currentPage: number;
  totalCount: number;
  totalPages: number;
  pageSize: number;
  /** Read-only on the backend; present in responses. */
  hasPreviousPage?: boolean;
  /** Read-only on the backend; present in responses. */
  hasNextPage?: boolean;
}

/**
 * Validation-failure shape. The backend returns HTTP 422 for validation
 * errors with the standard envelope (`successed: false`, populated `errors`).
 * This narrowed alias documents that callers should read `errors` on a 422.
 */
export type ValidationErrorResponse = BaseResponse<null> & {
  successed: false;
  errors: unknown[];
};

/** Unwrapped result that api-client query/mutation hooks expose to callers. */
export interface UnwrappedResult<T> {
  data: T;
  errors: unknown[];
}
