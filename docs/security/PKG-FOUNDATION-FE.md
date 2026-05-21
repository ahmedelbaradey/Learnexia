# Security Audit — PKG-FOUNDATION-FE Foundation packages (token handling + auth refresh)

**Type:** Light defensive pass (foundation code, no running app yet).
**Scope:** Client-side token-at-rest + auth refresh flow only. Backend, AI, file-upload, payments out of scope (no apps/backend deployed here).
**Date:** 2026-05-22

## Scope reviewed (files)
`packages/shared`:
- `src/storage/tokenStorage.ts` — `TokenStorage` interface, `TOKEN_STORAGE_KEYS`, `createInMemoryTokenStorage`
- `src/storage/native.ts` — `createNativeTokenStorage` (injected `expo-secure-store`)
- `src/storage/web.ts` — `createWebTokenStorage` (sessionStorage), `createCookieTokenStorageProxy`
- `src/storage/index.ts`
- `src/stores/authStore.ts`
- `src/types/auth.ts` (`CurrentUser`, token DTOs)

`packages/api-client`:
- `src/client/apiClient.ts` — JWT attach + 401 single-flight refresh + retry
- `src/client/config.ts`, `src/client/errors.ts`
- `src/query/queryClient.ts` — auth-aware retry
- `src/hooks/useSignIn.ts`
- `src/generated/api-types.ts`, `src/schemas.ts`, `src/index.ts`

## Findings

| # | Severity | Issue | Location | Remediation |
|---|----------|-------|----------|-------------|
| 1 | Low | Web default `sessionStorage` is readable by any JS in the origin, so it remains an XSS token-exfiltration target for both access AND refresh token. Choice is sound and well-documented as interim, but the long-lived refresh token in JS-readable storage is the larger residual risk. | `packages/shared/src/storage/web.ts:43-78` | Keep sessionStorage as interim. Plan to make `createCookieTokenStorageProxy` (HttpOnly refresh cookie) the default web strategy as soon as the backend cookie-auth path exists. Pair with a strict CSP in the web app to shrink XSS surface. |
| 2 | Low | `ApiEnvelopeError` / `ValidationError` propagate raw backend `message` + `errors` to the client unchanged. The backend's known `ServerError<T>(ex.Message)` pattern can return raw exception text; this client faithfully surfaces it to UI/logs. Not a client defect, but the client is the last line before display. | `packages/api-client/src/client/errors.ts:61-71`, `apiClient.ts:143-147,213-215` | No client change required for foundation. When UI is built, do not render raw `error.message` to children/users; map to friendly copy. Fix the info-disclosure at source on the backend (`ServerError`). |
| 3 | Info | In-memory token copy held in `authStore` (`accessToken`/`refreshToken` in Zustand state). Acceptable and intended for fast sync reads, but means tokens sit in the JS heap and any future devtools/state-logging middleware could expose them. | `packages/shared/src/stores/authStore.ts:26-27,57-60,69-73` | Do not attach persisting/logging middleware (e.g. redux-devtools serialization, persist) to `authStore` that would serialize tokens. Document this constraint. |
| 4 | Info | Refresh request omits an explicit `skipAuth`-equivalent guard; it builds its own fetch directly (good) but does not set a timeout/abort. A hung refresh holds the single-flight promise open, blocking all queued 401 retries until the platform fetch times out. | `packages/api-client/src/client/apiClient.ts:262-273` | Optional hardening: add an `AbortController` timeout to the refresh fetch so a stalled refresh fails fast and signs out rather than hanging the single-flight gate. |
| 5 | Info | `parseEnvelope` reads `res.text()` on every response including the refresh response; a non-JSON refresh body is caught and triggers sign-out (correct), but the caught text is discarded — confirm no future logging re-introduces it. | `packages/api-client/src/client/apiClient.ts:221-233,280-286` | No action now. Note for reviewers: never log the parsed text/envelope of the refresh response. |

## Positive verification (no findings)

- **Refresh token in body, not URL/header:** `RefreshRequestBody` = `{ accessToken, refreshToken }` sent as JSON POST body to `/api/Users/Authentication/Refresh-Token`; never appended to query string or a header. `config.ts:12-18`, `apiClient.ts:257-268`.
- **Single-flight coalescing:** one shared `refreshPromise`; concurrent 401s await it; cleared in `.finally`. No refresh storm. `apiClient.ts:56,241-248`.
- **Exactly one retry:** `isRetry` flag prevents a second refresh/retry; a 401 on the retried request falls through to `handleResponse` → `ApiAuthError`, no loop. `apiClient.ts:93-112,115-129`.
- **Refresh failure clears both tokens + signs out, no loop:** every failure branch (no tokens / network error / non-ok / parse error / unsuccessful envelope) calls `signOut()` → `tokenStorage.clear()` + `onSignOut`, returns `null`, caller throws `ApiAuthError`. `apiClient.ts:250-306`.
- **Sign-out completeness:** `authStore.signOut` clears storage AND resets `accessToken`/`refreshToken`/`user`/`status`. `authStore.ts:78-86`.
- **No token logging:** repo-wide grep found `console.*` only in `scripts/refresh-swagger.mjs` (build-time, no tokens) and comments. No logger touches token values. The interceptor explicitly never logs the attached Bearer token.
- **No `localStorage` path:** only referenced in `web.ts` comments explaining its deliberate avoidance. No code writes tokens to `localStorage`.
- **Abstraction leak-free:** `native.ts` takes `expo-secure-store` via injection (no static Expo import); `web.ts` resolves `sessionStorage` defensively off `globalThis` with SSR fallback. No platform import bleed across web/native. `apiClient`/`config` import only `@learnexia/shared` types — framework-agnostic.
- **Cookie proxy is a correct no-op holder:** `createCookieTokenStorageProxy` returns `null` tokens and no-op writes, relying on browser-attached HttpOnly cookie — JS cannot read the token. Sound design. `web.ts:88-101`.
- **Auth-aware retry:** `queryClient` never retries `ApiAuthError` or `ValidationError`; other failures retry up to 2x; mutations never retry. No auth retry loop. `queryClient.ts:22-32`.
- **422 → ValidationError** mapped before generic envelope error in both `handleResponse` and `requestPaginated`. `apiClient.ts:131-148,191-198`.
- **No secrets in generated contract:** `api-types.ts` contains only schema field names/types (e.g. `accessToken?: string | null`) — no example values, no `eyJ…` JWTs, no default/`CHANGE_ME` secrets baked in.
- **No PII overreach in store:** `CurrentUser` holds id/userName/fullName/email/roles/locale (identity, not server data); server profile owned by TanStack Query, not the store, per the state-split rule.

## Verdict: PASS-with-notes

No Critical or High findings. All findings are Low/Info hardening or interim-design notes. Single-flight refresh, one-retry, sign-out-on-failure, no-loop, and no-token-logging are all correctly implemented; storage abstraction is leak-free; generated contract carries no secrets.

## Notes / accepted risks
- Accepted interim risk: JWT (access + refresh) in JS-readable `sessionStorage` on web. Mitigated by the deliberate sessionStorage-over-localStorage choice. The migration trigger to make `createCookieTokenStorageProxy` (HttpOnly refresh cookie) the **default** web strategy is: backend ships the cookie-auth path. Track as a follow-up before the web student/admin app holds real child credentials in production.
- Backend `ServerError(ex.Message)` info-disclosure (finding #2 root cause) is a backend concern, filed against backend scope, not this package.
