# QC Test Plan & Coverage Report — P1-02 (Stay signed in: JWT refresh & sign-out)

> **Run scope:** BACKEND ONLY. No frontend surface in this story (P1-02-FE is a separate task). No `frontend-test-cases.md` produced.
> **Story:** `user-stories/Phase-1-Foundation/P1-02-stay-signed-in.md` · **Brief:** `docs/briefs/P1-02.md` · **Plan:** `docs/plans/P1-02.md` · **Task:** `tasks/Backend/Phase-1-Foundation/P1-02-BE.md`
> **Module:** Identity only · **FR:** FR-ID-4
> **Design pass owner:** QC test architect (design only — no test code, no execution).

---

## 1. Summary

P1-02 makes the Identity refresh-token / sign-out flow work end-to-end on top of the Redis (`IDistributedCache`) refresh-token store:

- **Sign-in / Register-Parent** now issue **and persist** a refresh token (`userrefreshtoken-{userId}`, 7-day TTL), returned on `JwtAuthResponse.refreshToken.tokenString`.
- **`POST /Refresh-Token`** (`[AllowAnonymous]`) validates the supplied refresh token **matches** the stored one, enforces the access-token-must-be-expired guard (`TokenIsRunning` → 400), enforces the 7-day expiry, **rotates** the token on success (new string returned, old invalidated), and maps all auth failures to **401** (not 500).
- **`POST /Sign-Out`** (`[Authorize]`) deletes `userrefreshtoken-{userId}` from Redis (primary revocation) and terminates the session by the `SessionId` claim, so the refresh token can no longer be exchanged.

### Endpoints under test

| Method | Route | Auth | Body | Success envelope |
|---|---|---|---|---|
| POST | `/api/Users/Authentication/Sign-In` | `[AllowAnonymous]` | `{ userName, password }` | `BaseResponse<JwtAuthResponse>` |
| POST | `/api/Users/Authentication/Register-Parent` | `[AllowAnonymous]` | `{ email, password, acceptedTerms }` | `BaseResponse<JwtAuthResponse>` |
| POST | `/api/Users/Authentication/Refresh-Token` | `[AllowAnonymous]` | `{ accessToken, refreshToken }` | `BaseResponse<JwtAuthResponse>` |
| POST | `/api/Users/Authentication/Sign-Out` | `[Authorize]` (Bearer) | `{}` (empty; identity from JWT) | `BaseResponse<string>` |

### Counts

| Metric | Count |
|---|---|
| **Total cases** | **28** |
| Backend (`api-tester`) | 28 |
| Frontend | 0 (out of scope) |
| **P0** | 17 |
| **P1** | 8 |
| **P2** | 3 |

By type: functional 6 · auth-authz 5 · negative 6 · boundary 4 · validation 3 · persistence 2 · regression 2.

> **Note on existing tests:** `backend/tests/Learnexia.IntegrationTests/P1_02_RefreshAndSignOut_Tests.cs` already exists and covers a large subset of these cases. This catalog is the **authoritative QC spec**; `api-tester` should map each `BE-TC-*` to a `[Fact]` (extending the existing file where a 1:1 test already exists, adding the gaps flagged below), so every case is traceable.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

The brief enumerates AC-1..AC-8 (testable refinement of the 3 story-level criteria). Story-level criteria are mapped first, then the brief's ACs.

### Story acceptance criteria (source of truth)

| Story criterion | Case IDs | Status |
|---|---|---|
| Valid refresh token → `POST /auth/refresh` returns a new access token | BE-TC-05, BE-TC-06, BE-TC-07 | Covered (P0) |
| Sign-out → refresh token invalidated (Redis), can no longer be exchanged | BE-TC-13, BE-TC-14, BE-TC-15 | Covered (P0) |
| Expired or revoked refresh token → 401, prompted to log in again | BE-TC-09, BE-TC-10, BE-TC-11, BE-TC-12, BE-TC-15, BE-TC-21 | Covered (P0) |

### Brief ACs (refined)

| AC | Description | Case IDs | Status |
|---|---|---|---|
| AC-1 | Valid (expired access + matching refresh) → 200 new non-empty access token (~30 min) | BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-18 | Covered |
| AC-2 | Sign-in & registration issue + persist a refresh token (`refreshToken.tokenString`) | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-04 | Covered |
| AC-3 | Sign-out invalidates refresh token in Redis; later refresh fails | BE-TC-13, BE-TC-14, BE-TC-15 | Covered |
| AC-4 | Expired/revoked refresh → **401 not 500**, `Successed=false` | BE-TC-09, BE-TC-10, BE-TC-11, BE-TC-12, BE-TC-15 | Covered |
| AC-5 | Refresh validates supplied token **matches** stored (no arbitrary-string accept) | BE-TC-08, BE-TC-09, BE-TC-21 | Covered |
| AC-6 | Token policy: access 30 min, refresh 7 days, expiry honored | BE-TC-18, BE-TC-19, BE-TC-20 | Covered |
| AC-7 | Missing `accessToken` / `refreshToken` → 422 with `Errors[]` | BE-TC-22, BE-TC-23, BE-TC-24 | Covered |
| AC-8 | Refresh token high-entropy + rotated on use (old no longer valid) | BE-TC-16, BE-TC-17, BE-TC-21 | Covered |

**Coverage verdict: PASS — every story criterion and every brief AC (AC-1..AC-8) has at least one P0/P1 case. No uncovered criterion.**

Additional cases beyond the ACs (defence-in-depth / envelope / negative / regression): BE-TC-12 (500-leakage guard), BE-TC-25/26 (Sign-Out auth), BE-TC-27 (Sign-Out idempotency), BE-TC-28 (P1-01 regression), BE-TC-10/11 (malformed/empty-vs-garbage token shapes).

---

## 3. Risk notes (where the cases are weighted, and why)

1. **401-vs-500 mapping (highest weight).** The brief's central defect was that `RefreshTokenNotFound` / `RefreshTokenIsExpired` / `AlgorithmIsWrong` returned `ServerError` (500). A 500 both breaks the FE "re-login" prompt and leaks internal state. Weighted P0 across BE-TC-09..12 and an explicit *never-500* assertion (BE-TC-12). The one **correct** non-401 negative is `TokenIsRunning` → **400** (BE-TC-18), which is easy to regress into a 401 or 500 — pinned separately.

2. **Refresh-token rotation & replay (security).** Rotation (AC-8) is the difference between a single-use and a reusable refresh token. The riskiest regression is a rotation that *returns* a new token but **fails to overwrite** the stored entry, leaving the old token replayable. BE-TC-16 (new ≠ old) and BE-TC-21 (replay of the original after rotation → 401) together pin both halves. Without BE-TC-21, BE-TC-16 alone passes even if the old token still works.

3. **Sign-out revocation must be load-bearing, not session-dependent.** The pre-existing `Jti`-vs-`SessionId` key mismatch meant session termination silently no-op'd. The plan's fix makes the **direct `RemoveAsync("userrefreshtoken-{userId}")`** the load-bearing guarantee, independent of session resolution. BE-TC-15 asserts the *observable* contract (post-sign-out refresh → 401) so the test passes regardless of which mechanism does the revoking — this is deliberately behavioural, not implementation-coupled.

4. **The "expired access token" test seam.** `ValidateDetails` requires the access token to be *already expired* to reach the happy path (otherwise `TokenIsRunning`/400). The existing test forges an expired-but-valid JWT by re-signing with the **known test secret** (`CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789` from `appsettings.json`). This seam is fragile: if the test secret changes or the host runs with a different signing key, every happy-path/rotation/revocation case silently degrades to the error path and the suite gives **false green**. See Open Question 1 — `api-tester` must guard this (assert the forged token actually reaches 200 in at least one case before relying on it elsewhere).

5. **Redis store is in-memory in tests.** `Program.cs` falls back to in-memory `IDistributedCache` when the Redis connection string is empty (`appsettings` `"Redis": ""`). The factory does not override it, so refresh/sign-out flows are single-process testable without a Redis container. Risk: TTL-based expiry (the real 7-day window) cannot be wall-clock tested; AC-6 expiry is asserted on the **returned `expireAt`** value and the stored `ExpiryDate` math, not by waiting (BE-TC-19, BE-TC-20). True expiry-driven 401 (BE-TC-11) is marked **partially blocked** — see §4.

6. **Live access-token window after sign-out (accepted residual).** Per Lead Decision 5, there is no per-request JTI denylist middleware, so an **already-issued** access token stays valid for up to its 30-min TTL after sign-out. This is out of scope and **not** asserted as a defect; BE-TC-13 only asserts the refresh token is killed, not the access token. Documented here so no tester writes a failing "access token rejected immediately after sign-out" case.

---

## 4. Open questions / assumptions (lead must resolve before/with implementation)

1. **Expired-JWT test seam vs the configured signing key.** *(Risk 4.)* The happy-path/rotation/revocation cases depend on forging an expired JWT signed with the **test** secret. **Assumption:** the Testing host uses the `appsettings.json` `JwtSettings.Secret` (`CHANGE_ME_...`) and `ValidateLifetime` does not block reading an expired token in `ReadJwtToken`. **Ask:** confirm the test signing key matches the host's, and that `api-tester` adds a guard asserting the forged token reaches 200 once, so a key drift fails loudly instead of silently routing to the 401 path (false green). *Blocks confidence in BE-TC-05, 06, 07, 16, 21, 15.*

2. **True refresh-token-expiry path (BE-TC-11).** Because the store is in-memory and the 7-day TTL cannot be wall-clock waited, the *expired stored refresh token* path (`ExpiryDate < UtcNow` → `RefreshTokenIsExpired` → 401) is **not directly reachable** via the public API in tests. **Assumption:** it is acceptable to cover the expiry-mapping via the *revoked* path (sign-out delete → not-found → 401, BE-TC-15) and a forged-token mismatch (BE-TC-09), and mark the pure-expiry 401 as **blocked (no time seam)**. **Ask:** is a clock/TTL seam in scope, or do we accept the `RefreshTokenIsExpired` branch being covered only by the existing `RefreshTokenValidatior` unit test + code review? *Affects BE-TC-11 status.*

3. **Sign-in input contract.** `SignInCommand` is `{ userName, password }` (not email). Tests sign in the seeded `superadmin` / `123Pa$$word!`. **Assumption:** parent accounts created via Register-Parent are addressable for refresh via the registration response's tokens (no separate parent sign-in needed for these cases). Confirm no story expectation that parents sign in by **email** (that would be a separate validation case).

4. **`Google-SignIn` refresh token.** `GetJwtToken` is the shared issuance path; Google-SignIn presumably also returns a refresh token now. **Assumption:** out of scope for P1-02 explicit assertion (Google is P1-12/P1-13 territory). Not catalogued. Confirm if the lead wants a smoke case.

5. **Validate-Token endpoint.** `POST /Validate-Token` exists (`[AllowAnonymous]`, `AccessTokenQuery`). It is **not** in P1-02's ACs (it is a query, not part of refresh/sign-out). **Assumption:** out of scope — not catalogued. Confirm.

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| `docs/qc/P1-02/backend-test-cases.md` | **`api-tester`** | Implement each `BE-TC-*` as a `[Fact]` in `backend/tests/Learnexia.IntegrationTests/P1_02_RefreshAndSignOut_Tests.cs` (extend the existing file; map 1:1; add the gap cases — BE-TC-10, 17, 19, 24, 27). Resolve Open Questions 1 & 2 (test-seam guard, expiry path) and record the decision in the test header. |
| `docs/qc/P1-02/execution-report.md` | **`api-tester`** (fills results) | Empty template provided. After running, fill pass/fail per case + defects. **QC never fills results.** |
| `docs/qc/P1-02/frontend-test-cases.md` | — | **Not produced** (backend-only run). |

**Execution-report flow:** `api-tester` runs `dotnet test`, then records per-case PASS/FAIL/BLOCKED + any defect in `execution-report.md`. The `reviewer` reads that report at the gate.

---

*Design-only pass. No test code written, no builds/tests run, no feature code edited.*
