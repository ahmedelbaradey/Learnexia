/**
 * Typed errors thrown by the api-client. Callers (and TanStack Query hooks)
 * branch on these instead of inspecting raw responses.
 *
 * SECURITY: error messages and fields never carry token values. The refresh
 * flow only ever surfaces `ApiAuthError` with a generic message.
 */

import type { BaseResponse } from '@learnexia/shared';

/** Base class for every error the client throws. */
export class ApiError extends Error {
  /** HTTP status code (0 if the request never reached the server). */
  readonly status: number;
  /** Backend `errors` array, when present. Never contains tokens. */
  readonly errors: unknown[];
  /** Backend `statusCode` echoed in the envelope, when present. */
  readonly bodyStatusCode?: number;

  constructor(
    message: string,
    status: number,
    errors: unknown[] = [],
    bodyStatusCode?: number,
  ) {
    super(message);
    this.name = 'ApiError';
    this.status = status;
    this.errors = errors;
    this.bodyStatusCode = bodyStatusCode;
  }
}

/**
 * HTTP 422 — the backend rejected the request on validation grounds. The
 * `errors` array carries field-level detail per the envelope contract.
 */
export class ValidationError extends ApiError {
  constructor(message: string, errors: unknown[], bodyStatusCode?: number) {
    super(message || 'Validation failed', 422, errors, bodyStatusCode);
    this.name = 'ValidationError';
  }
}

/**
 * Auth failure that the client could not recover from (401 with no/failed
 * refresh). The client clears tokens and signals sign-out before throwing this.
 * Generic message only — no token material.
 */
export class ApiAuthError extends ApiError {
  constructor(message = 'Authentication required', errors: unknown[] = []) {
    super(message, 401, errors);
    this.name = 'ApiAuthError';
  }
}

/**
 * The transport succeeded but the envelope reported failure
 * (`successed === false`) on a non-422, non-401 path.
 */
export class ApiEnvelopeError extends ApiError {
  constructor(envelope: BaseResponse<unknown>, status: number) {
    super(
      envelope.message ?? 'Request failed',
      status,
      (envelope.errors as unknown[]) ?? [],
      envelope.statusCode,
    );
    this.name = 'ApiEnvelopeError';
  }
}

export function isApiError(err: unknown): err is ApiError {
  return err instanceof ApiError;
}

export function isValidationError(err: unknown): err is ValidationError {
  return err instanceof ValidationError;
}

export function isApiAuthError(err: unknown): err is ApiAuthError {
  return err instanceof ApiAuthError;
}
