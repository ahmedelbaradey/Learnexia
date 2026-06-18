/**
 * The Learnexia HTTP client.
 *
 * Responsibilities:
 *  - attach `Authorization: Bearer <accessToken>` (read via the injected
 *    `TokenStorage` abstraction — never hard-bound to a platform);
 *  - unwrap the backend `BaseResponse<T>` envelope → return `data`, throw a
 *    typed error when `successed === false` or the transport fails;
 *  - map HTTP 422 to `ValidationError`;
 *  - on HTTP 401, perform a SINGLE-FLIGHT silent refresh against
 *    `POST /api/Users/Authentication/Refresh-Token` with body
 *    `{ accessToken, refreshToken }`, update tokens through the abstraction,
 *    retry the original request ONCE, and sign out on refresh failure.
 *
 * SECURITY: no token value is ever logged or placed in an error message. The
 * client logs nothing by default; the refresh path is silent.
 */

import type { AuthTokens, BaseResponse, PaginatedResult } from '@learnexia/shared';

import {
  ApiAuthError,
  ApiEnvelopeError,
  ApiError,
  ValidationError,
} from './errors';
import {
  REFRESH_TOKEN_PATH,
  type ApiClientConfig,
  type RefreshRequestBody,
} from './config';

export interface RequestOptions {
  /** Query params; `undefined`/`null` values are dropped. */
  query?: Record<string, string | number | boolean | undefined | null>;
  /** JSON body; serialized with `JSON.stringify`. */
  body?: unknown;
  /** Extra headers merged over defaults. */
  headers?: Record<string, string>;
  /** Skip attaching the Authorization header (e.g. sign-in). */
  skipAuth?: boolean;
  /** AbortSignal for cancellation (wired to TanStack Query). */
  signal?: AbortSignal;
}

type HttpMethod = 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';

export class ApiClient {
  private readonly config: ApiClientConfig;
  private readonly fetchImpl: typeof fetch;

  /**
   * Single in-flight refresh promise. Concurrent 401s await this one promise
   * instead of each firing their own refresh (single-flight coalescing).
   */
  private refreshPromise: Promise<AuthTokens | null> | null = null;

  constructor(config: ApiClientConfig) {
    this.config = config;
    this.fetchImpl = config.fetchImpl ?? globalThis.fetch.bind(globalThis);
  }

  /** API origin (no trailing slash). Used to construct the NSwag typed client. */
  get baseUrl(): string {
    return this.config.baseUrl.replace(/\/+$/, '');
  }

  get<T>(path: string, opts?: RequestOptions): Promise<T> {
    return this.request<T>('GET', path, opts);
  }
  post<T>(path: string, opts?: RequestOptions): Promise<T> {
    return this.request<T>('POST', path, opts);
  }
  put<T>(path: string, opts?: RequestOptions): Promise<T> {
    return this.request<T>('PUT', path, opts);
  }
  delete<T>(path: string, opts?: RequestOptions): Promise<T> {
    return this.request<T>('DELETE', path, opts);
  }
  patch<T>(path: string, opts?: RequestOptions): Promise<T> {
    return this.request<T>('PATCH', path, opts);
  }

  /**
   * GET a paginated endpoint. Unlike `get`, this returns the FULL envelope
   * (data + `currentPage`/`totalCount`/`totalPages`/`pageSize`) because paging
   * metadata lives alongside `data`, not inside it. Same 401-refresh + 422
   * handling as every other request.
   */
  getPaginated<T>(
    path: string,
    opts?: RequestOptions,
  ): Promise<PaginatedResult<T>> {
    return this.requestPaginated<T>('GET', path, opts);
  }

  /**
   * Transport seam for the NSwag-generated typed client.
   *
   * The generated `Client` (src/generated/nswag-client.ts) is constructed with
   * `new Client(baseUrl, { fetch })`. We pass THIS method as that `fetch` so
   * every generated endpoint method flows through the SAME pipeline as the
   * hand-written `get`/`post` helpers:
   *   - attaches `Authorization: Bearer <accessToken>` (unless the URL is the
   *     anonymous sign-in / register / refresh path);
   *   - performs the SINGLE-FLIGHT silent 401 refresh + ONE retry;
   *   - signs out on unrecoverable refresh failure.
   *
   * It returns the raw `Response` so the generated client can run its own typed
   * envelope parsing. Envelope semantics (`successed` flag, 422 → ValidationError)
   * are applied by `unwrapEnvelope()` in the typed-client wrapper, keeping a
   * single source of truth for both the hand-written and generated paths.
   *
   * `url` is absolute (the generated client prepends `baseUrl`); `init` carries
   * method/headers/body already serialized by the generator.
   */
  transportFetch = async (
    url: RequestInfo,
    init?: RequestInit,
  ): Promise<Response> => {
    const skipAuth = isAnonymousPath(typeof url === 'string' ? url : String(url));
    return this.transportRequest(url, init ?? {}, skipAuth, false);
  };

  private async transportRequest(
    url: RequestInfo,
    init: RequestInit,
    skipAuth: boolean,
    isRetry: boolean,
  ): Promise<Response> {
    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...this.config.defaultHeaders,
      ...normalizeHeaders(init.headers),
    };

    if (!skipAuth) {
      const tokens = await this.config.tokenStorage.getTokens();
      if (tokens?.accessToken) {
        // NOTE: token attached here is NEVER logged.
        headers.Authorization = `Bearer ${tokens.accessToken}`;
      }
    }

    const res = await this.fetchImpl(url, { ...init, headers });

    if (res.status === 401 && !skipAuth && !isRetry) {
      const refreshed = await this.refreshTokens();
      if (refreshed) {
        return this.transportRequest(url, init, skipAuth, true);
      }
      throw new ApiAuthError();
    }

    return res;
  }

  /**
   * Core request → returns the unwrapped `data`. Performs ONE retry after a
   * successful refresh on a 401.
   */
  private async request<T>(
    method: HttpMethod,
    path: string,
    opts: RequestOptions = {},
    isRetry = false,
  ): Promise<T> {
    const res = await this.rawFetch(method, path, opts);

    if (res.status === 401 && !opts.skipAuth && !isRetry) {
      const refreshed = await this.refreshTokens();
      if (refreshed) {
        // Retry exactly once with the new access token.
        return this.request<T>(method, path, opts, true);
      }
      // Refresh failed → tokens cleared + sign-out signalled in refreshTokens().
      throw new ApiAuthError();
    }

    return this.handleResponse<T>(res);
  }

  /** Paginated variant of `request` — returns the full `PaginatedResult`. */
  private async requestPaginated<T>(
    method: HttpMethod,
    path: string,
    opts: RequestOptions = {},
    isRetry = false,
  ): Promise<PaginatedResult<T>> {
    const res = await this.rawFetch(method, path, opts);

    if (res.status === 401 && !opts.skipAuth && !isRetry) {
      const refreshed = await this.refreshTokens();
      if (refreshed) {
        return this.requestPaginated<T>(method, path, opts, true);
      }
      throw new ApiAuthError();
    }

    const envelope = await this.parseEnvelope(res);

    if (res.status === 422) {
      throw new ValidationError(
        envelope?.message ?? 'Validation failed',
        (envelope?.errors as unknown[]) ?? [],
        envelope?.statusCode,
      );
    }
    if (res.status === 401) {
      throw new ApiAuthError(envelope?.message ?? 'Authentication required');
    }
    if (envelope === null || !res.ok || envelope.successed === false) {
      throw new ApiEnvelopeError(
        envelope ?? { statusCode: res.status, successed: false, data: null },
        res.status,
      );
    }

    return envelope as unknown as PaginatedResult<T>;
  }

  /** Build + execute a single fetch, attaching auth + headers. */
  private async rawFetch(
    method: HttpMethod,
    path: string,
    opts: RequestOptions,
  ): Promise<Response> {
    const url = this.buildUrl(path, opts.query);

    const headers: Record<string, string> = {
      Accept: 'application/json',
      ...this.config.defaultHeaders,
      ...opts.headers,
    };

    if (opts.body !== undefined) {
      headers['Content-Type'] = 'application/json';
    }

    if (!opts.skipAuth) {
      const tokens = await this.config.tokenStorage.getTokens();
      if (tokens?.accessToken) {
        // NOTE: token attached here is NEVER logged.
        headers.Authorization = `Bearer ${tokens.accessToken}`;
      }
    }

    return this.fetchImpl(url, {
      method,
      headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
      signal: opts.signal,
    });
  }

  /** Parse the envelope, map status codes to typed errors, return `data`. */
  private async handleResponse<T>(res: Response): Promise<T> {
    const envelope = await this.parseEnvelope(res);

    // 422 → validation error regardless of envelope.successed.
    if (res.status === 422) {
      throw new ValidationError(
        envelope?.message ?? 'Validation failed',
        (envelope?.errors as unknown[]) ?? [],
        envelope?.statusCode,
      );
    }

    if (res.status === 401) {
      throw new ApiAuthError(envelope?.message ?? 'Authentication required');
    }

    // No body (e.g. 204) — nothing to unwrap.
    if (envelope === null) {
      if (!res.ok) {
        throw new ApiError(`Request failed (${res.status})`, res.status);
      }
      return undefined as T;
    }

    // Envelope present: success flag is authoritative even on a 2xx.
    if (!res.ok || envelope.successed === false) {
      throw new ApiEnvelopeError(envelope, res.status);
    }

    return envelope.data as T;
  }

  /** Read + JSON-parse the body into a BaseResponse, tolerating empty bodies. */
  private async parseEnvelope(
    res: Response,
  ): Promise<BaseResponse<unknown> | null> {
    const text = await res.text();
    if (!text) return null;
    try {
      return JSON.parse(text) as BaseResponse<unknown>;
    } catch {
      // Non-JSON body. Surface a transport-level error; never echo raw text
      // that might contain sensitive material.
      throw new ApiError(`Unexpected non-JSON response (${res.status})`, res.status);
    }
  }

  /**
   * Single-flight silent refresh. Concurrent callers share one promise. On
   * success, tokens are written through storage (+ optional store hook) and
   * the new tokens returned. On failure, tokens are cleared and `onSignOut`
   * fires; returns null.
   */
  private refreshTokens(): Promise<AuthTokens | null> {
    if (this.refreshPromise) return this.refreshPromise;

    this.refreshPromise = this.performRefresh().finally(() => {
      this.refreshPromise = null;
    });
    return this.refreshPromise;
  }

  private async performRefresh(): Promise<AuthTokens | null> {
    const current = await this.config.tokenStorage.getTokens();
    if (!current?.accessToken || !current?.refreshToken) {
      await this.signOut();
      return null;
    }

    const body: RefreshRequestBody = {
      accessToken: current.accessToken,
      refreshToken: current.refreshToken,
    };

    let res: Response;
    try {
      res = await this.fetchImpl(this.buildUrl(REFRESH_TOKEN_PATH), {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Accept: 'application/json' },
        body: JSON.stringify(body),
      });
    } catch {
      // Network failure during refresh → unrecoverable. No token in message.
      await this.signOut();
      return null;
    }

    if (!res.ok) {
      await this.signOut();
      return null;
    }

    let envelope: BaseResponse<unknown> | null;
    try {
      envelope = await this.parseEnvelope(res);
    } catch {
      await this.signOut();
      return null;
    }

    const data = envelope?.successed ? extractTokens(envelope.data) : null;
    if (!data) {
      await this.signOut();
      return null;
    }

    await this.config.tokenStorage.setTokens(data);
    if (this.config.onTokensRefreshed) {
      await this.config.onTokensRefreshed(data);
    }
    return data;
  }

  private async signOut(): Promise<void> {
    await this.config.tokenStorage.clear();
    if (this.config.onSignOut) {
      await this.config.onSignOut();
    }
  }

  private buildUrl(
    path: string,
    query?: RequestOptions['query'],
  ): string {
    const base = this.config.baseUrl.replace(/\/+$/, '');
    const rel = path.startsWith('/') ? path : `/${path}`;
    let url = `${base}${rel}`;
    if (query) {
      const params = new URLSearchParams();
      for (const [key, value] of Object.entries(query)) {
        if (value !== undefined && value !== null) {
          params.append(key, String(value));
        }
      }
      const qs = params.toString();
      if (qs) url += `?${qs}`;
    }
    return url;
  }
}

/**
 * Pull `{ accessToken, refreshToken }` out of the refresh response payload.
 * The backend `JwtAuthResponse` nests the refresh token as
 * `refreshToken: { tokenString }`, so we normalize to flat strings.
 */
function extractTokens(data: unknown): AuthTokens | null {
  if (!data || typeof data !== 'object') return null;
  const obj = data as {
    accessToken?: unknown;
    refreshToken?: unknown;
  };
  const accessToken =
    typeof obj.accessToken === 'string' ? obj.accessToken : null;

  let refreshToken: string | null = null;
  if (typeof obj.refreshToken === 'string') {
    refreshToken = obj.refreshToken;
  } else if (obj.refreshToken && typeof obj.refreshToken === 'object') {
    const nested = (obj.refreshToken as { tokenString?: unknown }).tokenString;
    if (typeof nested === 'string') refreshToken = nested;
  }

  if (!accessToken || !refreshToken) return null;
  return { accessToken, refreshToken };
}

/**
 * Anonymous endpoints that must NOT carry an Authorization header (and must NOT
 * trigger the 401-refresh retry, since they ARE the auth bootstrap). Matched by
 * path suffix so the absolute URLs the NSwag client builds still resolve.
 *
 * Transport fix (P1-12-FE FE-0a): Google-SignIn, Forgot-Password, and
 * Reset-Password are unauthenticated endpoints. Without this list they would
 * attach a stale bearer token and trigger the single-flight 401-refresh retry,
 * breaking the flows. Verified against nswag-client.ts lines 1137/1344/1413.
 */
const ANONYMOUS_PATH_SUFFIXES = [
  '/api/Users/Authentication/Sign-In',
  '/api/Users/Authentication/Register-Parent',
  '/api/Users/Authentication/Refresh-Token',
  '/api/Users/Authentication/Google-SignIn',
  '/api/Users/Authentication/Forgot-Password',
  '/api/Users/Authentication/Reset-Password',
];

function isAnonymousPath(url: string): boolean {
  // Drop any query string before matching the path suffix.
  const path = url.split('?')[0] ?? url;
  return ANONYMOUS_PATH_SUFFIXES.some((suffix) => path.endsWith(suffix));
}

/** Normalize the various `HeadersInit` shapes into a plain record. */
function normalizeHeaders(
  headers: HeadersInit | undefined,
): Record<string, string> {
  if (!headers) return {};
  if (headers instanceof Headers) {
    const out: Record<string, string> = {};
    headers.forEach((value: string, key: string) => {
      out[key] = value;
    });
    return out;
  }
  if (Array.isArray(headers)) {
    return Object.fromEntries(headers);
  }
  return { ...(headers as Record<string, string>) };
}

/** Factory matching the rest of the @learnexia/shared `createX` convention. */
export function createApiClient(config: ApiClientConfig): ApiClient {
  return new ApiClient(config);
}
