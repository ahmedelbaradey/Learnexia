/**
 * typedClient — bridges the NSwag-generated `Client` to the existing
 * `ApiClient` transport and to our `BaseResponse` envelope semantics.
 *
 * WHY THIS LAYER EXISTS
 * ---------------------
 * The NSwag-generated client (`src/generated/nswag-client.ts`) gives us a
 * fully typed method per endpoint + DTO interfaces. But on its own it:
 *   - knows nothing about auth headers or the single-flight 401 refresh, and
 *   - returns the FULL `*BaseResponse` envelope (and `throw`s `ApiException`
 *     on non-2xx).
 *
 * We keep BOTH concerns single-sourced in the hand-written `ApiClient`:
 *   - `apiClient.transportFetch` is injected as the generated client's `http`,
 *     so every typed method flows through the same auth + 401-refresh pipeline
 *     as the legacy `get`/`post` helpers (the interceptor is preserved).
 *   - `unwrapEnvelope` applies the SAME envelope rules as
 *     `ApiClient.handleResponse`: it returns `data` on success, maps 422 →
 *     `ValidationError`, 401 → `ApiAuthError`, and a `successed === false`
 *     envelope → `ApiEnvelopeError`. It also translates NSwag's `ApiException`
 *     (thrown on 4xx/5xx) into our typed errors.
 *
 * Hooks call the typed client methods and unwrap with `unwrapEnvelope`, so they
 * stay thin TanStack Query wrappers over the generated client while their public
 * signatures (e.g. `useSignIn`) are unchanged.
 */

import type { BaseResponse } from '@learnexia/shared';

import { GeneratedApiClient, ApiException } from '../generated';
import type { ApiClient } from './apiClient';
import {
  ApiAuthError,
  ApiEnvelopeError,
  ApiError,
  ValidationError,
} from './errors';

export type TypedApiClient = GeneratedApiClient;

/**
 * Build the NSwag typed client wired to the given `ApiClient` transport.
 * The generated `Client` prepends `baseUrl` to each path, so we pass an empty
 * base and let `transportFetch` receive absolute-from-base URLs — except the
 * base must actually be the API origin. We therefore read it off the client.
 */
export function createTypedClient(apiClient: ApiClient): TypedApiClient {
  return new GeneratedApiClient(apiClient.baseUrl, {
    fetch: apiClient.transportFetch,
  });
}

/** Any generated `*BaseResponse` envelope (its `data` is the payload). */
type GeneratedEnvelope<TData> = { successed?: boolean; statusCode?: unknown; message?: string; errors?: unknown[]; data?: TData };

/**
 * Apply `BaseResponse` envelope semantics to a generated-client result and
 * return the unwrapped `data`. Mirrors `ApiClient.handleResponse`.
 *
 * Infers the payload type from the envelope's own `data` field, so call sites
 * read cleanly: `unwrapEnvelope(client.signIn(input))` → `Promise<JwtAuthResponse>`.
 *
 * The generated client already threw `ApiException` for non-2xx; we translate
 * those here. For 2xx envelopes we honor the `successed` flag.
 */
export function unwrapEnvelope<TData>(
  promise: Promise<GeneratedEnvelope<TData>>,
): Promise<TData> {
  return promise.then(
    (envelope) => {
      if (envelope == null) {
        // 204 / empty body — nothing to unwrap.
        return undefined as TData;
      }
      if (envelope.successed === false) {
        throw new ApiEnvelopeError(
          envelope as BaseResponse<unknown>,
          (envelope.statusCode as number) ?? 0,
        );
      }
      return envelope.data as TData;
    },
    (err: unknown) => {
      throw translateError(err);
    },
  );
}

/** Convert NSwag `ApiException` (or any thrown value) into our typed errors. */
function translateError(err: unknown): Error {
  if (ApiException.isApiException(err)) {
    const { status, result, response, message } = err;
    if (status === 422) {
      const envelope = (result ?? safeParse(response)) as
        | BaseResponse<unknown>
        | null;
      return new ValidationError(
        envelope?.message ?? 'Validation failed',
        (envelope?.errors as unknown[]) ?? [],
        envelope?.statusCode as number | undefined,
      );
    }
    if (status === 401) {
      const envelope = (result ?? safeParse(response)) as
        | BaseResponse<unknown>
        | null;
      return new ApiAuthError(envelope?.message ?? 'Authentication required');
    }
    // Any other non-2xx: prefer the envelope (so callers can read errors[]).
    const envelope = (result ?? safeParse(response)) as
      | BaseResponse<unknown>
      | null;
    if (envelope && typeof envelope === 'object' && 'successed' in envelope) {
      return new ApiEnvelopeError(envelope, status);
    }
    return new ApiError(message || `Request failed (${status})`, status);
  }
  // ApiAuthError is thrown directly by transportFetch on unrecoverable refresh.
  if (err instanceof Error) return err;
  // NSwag's generated `throwException` throws the PARSED response body (not an
  // ApiException) whenever the endpoint declares a result type for that status
  // code — so a 4xx/5xx `BaseResponse` envelope arrives here as a plain object.
  // Map it to our typed errors so callers get the real status + message/errors
  // instead of an opaque "Unknown request error".
  if (err && typeof err === 'object') {
    const env = err as GeneratedEnvelope<unknown> & { errors?: unknown[] };
    if ('successed' in env || 'statusCode' in env || 'message' in env) {
      const status = (env.statusCode as number) ?? 0;
      if (status === 422) {
        return new ValidationError(
          env.message ?? 'Validation failed',
          (env.errors as unknown[]) ?? [],
          status,
        );
      }
      if (status === 401) {
        return new ApiAuthError(env.message ?? 'Authentication required');
      }
      return new ApiEnvelopeError(env as BaseResponse<unknown>, status);
    }
  }
  return new ApiError('Unknown request error', 0);
}

function safeParse(text: string): unknown {
  if (!text) return null;
  try {
    return JSON.parse(text);
  } catch {
    return null;
  }
}
