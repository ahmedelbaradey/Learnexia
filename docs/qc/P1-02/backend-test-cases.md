# Backend Test Cases — P1-02 (Stay signed in: refresh & sign-out)

> **Target agent:** `api-tester` · **Surface:** Identity `AuthenticationController` (integration, against the running test host).
> **Harness:** `LearnexiaWebAppFactory` (Testcontainers Postgres + in-memory `IDistributedCache`). Pattern: `P1_02_RefreshAndSignOut_Tests.cs` / `P1_01_RegisterParent_Tests.cs`.
> **Envelope:** `BaseResponse<T>` — keys `statusCode`, `succeeded`-spelled-**`successed`**, `message`, `errors`, `data`. Controller path (200/400/401) = camelCase (Newtonsoft); the 422 validation path = PascalCase (System.Text.Json). Use the case-insensitive `TryProp` helper for both.
> **Seed accounts:** `superadmin` / `123Pa$$word!` (sign-in); new parents via `Register-Parent { email, password:"Str0ng@Pass", acceptedTerms:true }`.
> **Expired-JWT seam (load-bearing):** the happy/rotation/revocation cases need an *already-expired but validly-signed* access token. Forge it by re-signing the issued token's claims (minus `exp`/`iat`/`nbf`) with the test secret `CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789` and a past `exp`. **Guard:** BE-TC-05 must prove this seam reaches 200 before later cases rely on it (see README Open Q1).

Routes (all under `/api/Users/Authentication`): `Sign-In`, `Register-Parent`, `Refresh-Token`, `Sign-Out`.

Legend — Type / Priority / Traces. Every case targets `api-tester`. Preconditions name the seed.

---

## Group A — Refresh-token issuance & persistence (AC-2)

### BE-TC-01 — Sign-In returns a non-empty refresh token
- **Type:** functional · **Priority:** P0 · **Traces:** AC-2, Story (refresh prerequisite)
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. POST `/Sign-In` `{ userName:"superadmin", password:"123Pa$$word!" }`.
- **Expected:** 200; `successed=true`; `data.refreshToken.tokenString` present and **non-empty**; `data.accessToken` non-empty.

### BE-TC-02 — Register-Parent returns a non-empty refresh token
- **Type:** functional · **Priority:** P0 · **Traces:** AC-2
- **Preconditions:** none (unique email).
- **Steps:**
  1. POST `/Register-Parent` `{ email:<unique>, password:"Str0ng@Pass", acceptedTerms:true }`.
- **Expected:** 200; `successed=true`; `data.refreshToken.tokenString` present and **non-empty**.

### BE-TC-03 — Refresh token is persisted/retrievable (round-trips through Redis)
- **Type:** persistence · **Priority:** P0 · **Traces:** AC-2
- **Preconditions:** none.
- **Steps:**
  1. Register a parent → capture `accessToken`, `refreshToken`.
  2. Forge an expired access token from the issued one.
  3. POST `/Refresh-Token` `{ accessToken:<expired>, refreshToken:<captured> }`.
- **Expected:** 200 — proves the token issued at sign-in was actually written to the store and is matchable later (not merely returned in the body and discarded).

### BE-TC-04 — Refresh token carries a 7-day `expireAt`
- **Type:** boundary · **Priority:** P1 · **Traces:** AC-2, AC-6
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. POST `/Sign-In`.
  2. Read `data.refreshToken.expireAt`.
- **Expected:** `expireAt` ≈ `UtcNow + 7 days` (tolerance ±60 s).

---

## Group B — Refresh happy path & new access token (AC-1, AC-6)

### BE-TC-05 — Valid expired access + matching refresh → 200 new access token *(seam-validating)*
- **Type:** functional · **Priority:** P0 · **Traces:** AC-1, Story
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. Sign in → capture `accessToken`, `refreshToken`.
  2. Forge an expired access token from the issued one.
  3. POST `/Refresh-Token` `{ accessToken:<expired>, refreshToken:<captured> }`.
- **Expected:** **200**; `successed=true`; `data.accessToken` non-empty **and different** from the original. (This is the canary for the expired-JWT seam — if it 401s, the seam is broken; fail loudly, do not skip dependent cases.)

### BE-TC-06 — New access token is a readable, valid JWT
- **Type:** functional · **Priority:** P1 · **Traces:** AC-1
- **Preconditions:** as BE-TC-05.
- **Steps:**
  1. Perform a successful refresh (BE-TC-05 flow).
  2. `JwtSecurityTokenHandler.CanReadToken(newAccessToken)`.
- **Expected:** token is readable; carries the same `Id`/identity claims as the original; `successed=true`.

### BE-TC-07 — Refresh preserves identity claims (same user)
- **Type:** functional · **Priority:** P1 · **Traces:** AC-1
- **Preconditions:** as BE-TC-05.
- **Steps:**
  1. Successful refresh; decode the new access token.
- **Expected:** new token's `Id` / `NameIdentifier` claims equal the original user's — the refresh re-issues for the same principal, not a different one.

---

## Group C — Refresh validation & match enforcement (AC-5)

### BE-TC-08 — Tampered refresh token (one char off) → 401
- **Type:** negative · **Priority:** P0 · **Traces:** AC-5, Story
- **Preconditions:** register a parent; capture matching token pair.
- **Steps:**
  1. Forge expired access token.
  2. Mutate the last char of the refresh token string.
  3. POST `/Refresh-Token` `{ accessToken:<expired>, refreshToken:<tampered> }`.
- **Expected:** **401**; `successed=false`. (Pins that a cache entry existing for the user is not sufficient — the supplied string must equal the stored one.)

### BE-TC-09 — Arbitrary garbage refresh token → 401 (not 500)
- **Type:** negative · **Priority:** P0 · **Traces:** AC-4, AC-5, Story
- **Preconditions:** seeded `superadmin` signed in (so a stored entry exists for that user).
- **Steps:**
  1. Forge expired access token.
  2. POST `/Refresh-Token` `{ accessToken:<expired>, refreshToken:"this-is-garbage-and-will-not-match" }`.
- **Expected:** **401**; `successed=false`. Explicitly assert status `!= 500`.

### BE-TC-10 — Refresh for a user with NO stored token → 401 (`RefreshTokenNotFound`)
- **Type:** negative · **Priority:** P1 · **Traces:** AC-4, AC-5 · **GAP — add**
- **Preconditions:** a freshly-registered parent whose stored refresh token has been removed (sign that parent out first, OR use a forged access token for a user id that never refreshed).
- **Steps:**
  1. Register parent → access + refresh.
  2. Sign out (clears the stored entry).
  3. Forge expired access token; POST `/Refresh-Token` with the original (now storeless) refresh token.
- **Expected:** **401** `RefreshTokenNotFound` path; `successed=false`. (Distinguishes the *no stored entry* branch from the *mismatch* branch — both must 401, never 500.)

### BE-TC-11 — Expired stored refresh token → 401 (`RefreshTokenIsExpired`)
- **Type:** boundary · **Priority:** P1 · **Traces:** AC-4, AC-6, Story · **BLOCKED (no time seam) — see README Open Q2**
- **Preconditions:** a stored refresh token whose `ExpiryDate` is in the past.
- **Steps:** (not reachable via public API without a clock/TTL seam — the 7-day window cannot be wall-clock waited in-suite).
- **Expected:** **401** `RefreshTokenIsExpired`; `successed=false`.
- **Blocker:** no test seam to age the stored `ExpiryDate`. Covered indirectly by the revoked path (BE-TC-15) and the unit test `RefreshTokenValidatiorTests`. **Mark BLOCKED in the execution report** unless the lead adds a seam.

---

## Group D — Error mapping & envelope (AC-4)

### BE-TC-12 — `/Refresh-Token` NEVER returns 500 for auth failures
- **Type:** negative · **Priority:** P0 · **Traces:** AC-4 (core defect)
- **Preconditions:** seeded `superadmin` signed in.
- **Steps:**
  1. Forge expired access token; POST `/Refresh-Token` with a garbage refresh token.
- **Expected:** status is **401**, asserted `!= 500`; the response is a well-formed `BaseResponse` envelope with `statusCode`, `successed=false`, `message` present.

### BE-TC-18 — `TokenIsRunning`: refresh with a still-valid (non-expired) access token → 400 (not 401, not 500)
- **Type:** boundary · **Priority:** P0 · **Traces:** AC-6, AC-1 (guard)
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. Sign in → capture the **fresh, unexpired** access token + refresh token.
  2. POST `/Refresh-Token` `{ accessToken:<fresh, unexpired>, refreshToken:<captured> }`.
- **Expected:** **400 BadRequest** ("Token is not expired."); `successed=false`; assert `!= 500` and `!= 401`. (Pins the deliberate non-401 negative: refreshing too early is a caller error, not an auth failure. Also guards the `DateTime.UtcNow` fix — a local-time regression would make a fresh token look expired and spuriously 200.)

---

## Group E — Sign-out & revocation (AC-3)

### BE-TC-13 — Sign-Out with Bearer → 200 `Successed=true`
- **Type:** functional · **Priority:** P0 · **Traces:** AC-3, Story
- **Preconditions:** register a parent; capture access token.
- **Steps:**
  1. POST `/Sign-Out` empty body with `Authorization: Bearer <accessToken>`.
- **Expected:** 200; `successed=true`.

### BE-TC-14 — Sign-Out persists revocation (stored refresh entry removed)
- **Type:** persistence · **Priority:** P0 · **Traces:** AC-3
- **Preconditions:** register a parent; capture token pair.
- **Steps:**
  1. Sign out (Bearer).
  2. Forge expired access token; POST `/Refresh-Token` with the captured refresh token.
- **Expected:** the refresh **fails** (401) — observable proof the stored entry was deleted.

### BE-TC-15 — Revoked (signed-out) refresh token → 401 (cannot be re-exchanged)
- **Type:** auth-authz · **Priority:** P0 · **Traces:** AC-3, AC-4, Story
- **Preconditions:** register a parent; capture token pair.
- **Steps:**
  1. POST `/Sign-Out` (Bearer) → 200.
  2. Forge expired access token; POST `/Refresh-Token` `{ accessToken:<expired>, refreshToken:<pre-signout> }`.
- **Expected:** **401**; `successed=false`. (Behavioural contract; passes regardless of whether revocation came from the direct `RemoveAsync` or session termination.)

---

## Group F — Rotation & replay (AC-8)

### BE-TC-16 — Rotation: refreshed token string differs from the supplied one
- **Type:** functional · **Priority:** P0 · **Traces:** AC-8
- **Preconditions:** sign in / register; capture `oldRefresh`.
- **Steps:**
  1. Forge expired access token.
  2. POST `/Refresh-Token` with `oldRefresh` → 200.
  3. Read `data.refreshToken.tokenString` (the new one).
- **Expected:** new `tokenString` non-empty and **≠ `oldRefresh`**.

### BE-TC-17 — Rotation: new refresh token also carries a 7-day `expireAt`
- **Type:** boundary · **Priority:** P2 · **Traces:** AC-6, AC-8 · **GAP — add**
- **Preconditions:** as BE-TC-16.
- **Steps:**
  1. Perform a successful refresh; read `data.refreshToken.expireAt`.
- **Expected:** rotated `expireAt` ≈ `UtcNow + 7 days` (±60 s) — rotation re-issues a full fresh window, not a residual one.

### BE-TC-21 — Replay: original refresh token after rotation → 401
- **Type:** negative · **Priority:** P0 · **Traces:** AC-8, AC-5 (security)
- **Preconditions:** register a parent; capture `originalRefresh`.
- **Steps:**
  1. Forge expired access token; POST `/Refresh-Token` with `originalRefresh` → 200; capture `newAccess`.
  2. Forge expired access token from `newAccess`; POST `/Refresh-Token` again with **`originalRefresh`** (the rotated-away token).
- **Expected:** second call → **401**; `successed=false`. (Without this, BE-TC-16 passes even if the store was not overwritten.)

---

## Group G — Token policy (AC-6)

### BE-TC-19 — Access token lifetime ≈ 30 min
- **Type:** boundary · **Priority:** P1 · **Traces:** AC-6 · **GAP — confirm present**
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. Capture issuance window (`before`/`after` `UtcNow`); sign in; decode the access token.
  2. Compute `ValidTo - issuance midpoint`.
- **Expected:** ≈ 30 min (tolerance ±2 min to absorb host-clock offset).

### BE-TC-20 — Refresh token lifetime ≈ 7 days (sign-in)
- **Type:** boundary · **Priority:** P1 · **Traces:** AC-6
- **Preconditions:** seeded `superadmin`.
- **Steps:**
  1. Sign in; read `data.refreshToken.expireAt`.
- **Expected:** `expireAt` ≈ `UtcNow + 7 days` (±60 s).

---

## Group H — Request validation (AC-7)

### BE-TC-22 — Missing `accessToken` → 422 with `Errors[]`
- **Type:** validation · **Priority:** P0 · **Traces:** AC-7
- **Steps:** POST `/Refresh-Token` `{ accessToken:"", refreshToken:"some-token" }`.
- **Expected:** **422**; `successed=false`; `errors` array non-empty; each item has `propertyName` + `errorMessage`. (PascalCase path — use `TryProp`.)

### BE-TC-23 — Missing `refreshToken` → 422 with `Errors[]`
- **Type:** validation · **Priority:** P0 · **Traces:** AC-7
- **Steps:** POST `/Refresh-Token` `{ accessToken:"some-token", refreshToken:"" }`.
- **Expected:** **422**; `successed=false`; `errors` non-empty.

### BE-TC-24 — Both fields missing → 422, one error per field
- **Type:** validation · **Priority:** P1 · **Traces:** AC-7 · **GAP — add**
- **Steps:** POST `/Refresh-Token` `{ accessToken:"", refreshToken:"" }`.
- **Expected:** **422**; `errors` contains at least one entry; entries reference both `AccessToken` and `RefreshToken`.

---

## Group I — Sign-Out authorization & idempotency

### BE-TC-25 — Sign-Out without a Bearer token → 401
- **Type:** auth-authz · **Priority:** P0 · **Traces:** Story (sign-out is `[Authorize]`)
- **Steps:** POST `/Sign-Out` empty body, **no** `Authorization` header.
- **Expected:** **401** (framework-level; `[Authorize]` rejects before the handler).

### BE-TC-26 — Sign-Out with a malformed/garbage Bearer token → 401
- **Type:** auth-authz · **Priority:** P1 · **Traces:** Story · **GAP — add**
- **Steps:** POST `/Sign-Out` with `Authorization: Bearer not-a-real-jwt`.
- **Expected:** **401** — an unparseable/invalid token is rejected by JWT auth, not 500.

### BE-TC-27 — Sign-Out is idempotent (second sign-out still 200, no 500)
- **Type:** negative · **Priority:** P2 · **Traces:** AC-3 (robustness) · **GAP — add**
- **Preconditions:** register a parent; capture access token (note: same access token is reusable until its 30-min TTL — the live-token window per Lead Decision 5).
- **Steps:**
  1. POST `/Sign-Out` (Bearer) → 200.
  2. POST `/Sign-Out` again with the **same** still-valid access token.
- **Expected:** second call → **200** (or a clean 401 if the access token is rejected), **never 500** — `RemoveAsync` on an absent key must not throw.

---

## Group J — Regression

### BE-TC-28 — P1-01 sign-in still works after P1-02 changes
- **Type:** regression · **Priority:** P1 · **Traces:** regression (no AC broken)
- **Steps:** POST `/Sign-In` `{ userName:"superadmin", password:"123Pa$$word!" }`.
- **Expected:** 200; `successed=true`; `data.accessToken` non-empty. (Pins that adding refresh-token issuance did not break the existing P1-01 sign-in contract.)

---

## Implementation notes for `api-tester`

- **Map 1:1.** Most cases already exist in `P1_02_RefreshAndSignOut_Tests.cs`. Extend that file; do not duplicate the harness. Add the **GAP** cases: BE-TC-10, BE-TC-17, BE-TC-19 (confirm), BE-TC-24, BE-TC-26, BE-TC-27.
- **Seam guard (Open Q1).** Treat BE-TC-05 as the canary: if it does not reach 200, the expired-JWT forge is broken — surface that as a loud failure, not a silent route to the 401 cases.
- **BLOCKED case.** BE-TC-11 (pure refresh-token-expiry 401) has no time seam — mark it **BLOCKED** in `execution-report.md` with the reason, unless the lead adds a clock/TTL seam.
- **Envelope keys.** `successed` (sic) for the success flag; `errors` (array of `{ propertyName, errorMessage }`) only on the 422 path. Use the case-insensitive `TryProp` to span the Newtonsoft (camelCase) vs System.Text.Json (PascalCase) split.
- Record each `BE-TC-*` → `[Fact]` name mapping in the execution report so the reviewer can trace coverage.
