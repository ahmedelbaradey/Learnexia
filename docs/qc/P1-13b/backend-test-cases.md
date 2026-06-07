# P1-13b — Backend test cases (for `api-tester`)

> **Scope:** P1-13b **BE-1 only** — per-endpoint IP rate-limiting on the anonymous auth endpoints. Surface under test: `ConfigureRateLimitingOptions(...)` in `backend/src/Host/Learnexia.Host/Extensions/ServiceExtensions.cs` + `app.UseIpRateLimiting()` in `Program.cs`. All other P1-13b items are relocated to P6-06 and are **out of scope** here (see README §2/§4).

## Implementation guidance (read before writing tests)

- **Reuse the existing deterministic harness.** An integration suite already exists: `backend/tests/Learnexia.IntegrationTests/P1_13b_BE1_AuthRateLimit_Tests.cs` with `RateLimitWebAppFactory` (separate Postgres DB `LearnexiaRateLimit`, separate xUnit `[Collection("RateLimitTests")]`). It overrides `IpRateLimitOptions` via `PostConfigure<>` to a tiny `Limit=2/1m` on sign-in so the 3rd call deterministically 429s. **Reuse this pattern** — do **not** fire 100 real requests/sec.
- **Reconcile, don't duplicate.** Existing AC-RL-1..6 already cover: 429 on exceed (sign-in), under-limit passes, both `/health` endpoints whitelisted, register-parent isolation, non-empty 429 body, valid sign-in 200. Where a case below maps to an existing AC, **extend** the same file; only add new factory rules where a case needs another endpoint constrained.
- **Counter isolation per test:** key the in-memory counter by a **unique spoofed IP per test** (`X-Real-IP: 10.x.y.z` with random octets), as the existing tests do, so parallel/collection-shared runs don't pollute counters.
- **Route casing:** rules use lowercased `"{verb}:{path}"` (e.g. `post:/api/users/authentication/forgot-password`); request URLs may be mixed-case (`/api/Users/Authentication/Forgot-Password`) — routing is case-insensitive, matching works.
- **Envelope:** success bodies are `BaseResponse<T>` with the success flag spelled **`Successed`** (do not assert `Success`). 429 bodies come from AspNetCoreRateLimit (plain text / JSON), **not** the `BaseResponse` envelope — assert non-empty, not envelope shape.

---

## Group A — 429 on exceed, per anonymous endpoint (AC-1)

### BE-TC-01 — Sign-In: exceeding the limit returns 429
- **Type:** functional / security (DoS ceiling)
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** `RateLimitWebAppFactory` with PostConfigure rule `post:/api/users/authentication/sign-in = Limit=2/1m`; global `* = int.MaxValue`. Unique spoofed `X-Real-IP`. No auth (anonymous).
- **Steps:**
  1. POST `/api/Users/Authentication/Sign-In` with invalid creds (`{UserName:"notexist", Password:"WrongPass1@"}`). Record status.
  2. Repeat once more (2nd call).
  3. POST a 3rd time from the same IP.
- **Expected result:** Calls 1 & 2 return a non-429 status (400 or 401 from the handler — bad creds). **Call 3 returns HTTP 429 Too Many Requests.**
- **Traces to:** AC-1 (per-endpoint 429 on exceed). *(Maps to existing AC-RL-1 — reconcile.)*

### BE-TC-02 — Register-Parent: exceeding the limit returns 429
- **Type:** functional / security (anti-spam registration)
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory PostConfigure adds `post:/api/users/authentication/register-parent = Limit=2/1m`; global `* = int.MaxValue`; unique spoofed IP.
- **Steps:**
  1. POST `/api/Users/Authentication/Register-Parent` with a malformed body (e.g. `{Email:"not-an-email", Password:"weak", AcceptedTerms:true}`) twice — handler returns 422 (validation).
  2. POST a 3rd time from the same IP.
- **Expected result:** Calls 1 & 2 → 422 (handler reached). **Call 3 → 429.** (Confirms the per-endpoint rule fires even when the body is invalid — rate limit is evaluated before/independently of validation.)
- **Traces to:** AC-1.

### BE-TC-03 — Forgot-Password: exceeding the limit returns 429 (highest abuse target)
- **Type:** negative / security (enumeration / reset-spam ceiling)
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory PostConfigure adds `post:/api/users/authentication/forgot-password = Limit=2/1m`; global `* = int.MaxValue`; unique spoofed IP.
- **Steps:**
  1. POST `/api/Users/Authentication/Forgot-Password` with `{Email:"someone@example.com"}` twice. (Generic 200 expected regardless of account existence.)
  2. POST a 3rd time from the same IP.
- **Expected result:** Calls 1 & 2 → generic 200 (no enumeration). **Call 3 → 429.** Confirms rate limiting fires on the anonymous reset-request endpoint before the (out-of-band) email path.
- **Traces to:** AC-1.

### BE-TC-04 — Reset-Password: exceeding the limit returns 429
- **Type:** negative / security (reset-token spray ceiling)
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory PostConfigure adds `post:/api/users/authentication/reset-password = Limit=2/1m`; global `* = int.MaxValue`; unique spoofed IP.
- **Steps:**
  1. POST `/api/Users/Authentication/Reset-Password` with a junk body (bad email + bad token) twice — handler returns the generic failure.
  2. POST a 3rd time from the same IP.
- **Expected result:** Calls 1 & 2 → generic failure status (not 429). **Call 3 → 429.** Confirms the reset endpoint is throttled (blocks brute-forcing reset tokens beyond the per-IP ceiling).
- **Traces to:** AC-1.

### BE-TC-05 — Google-SignIn: exceeding the limit returns 429
- **Type:** functional / security
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory PostConfigure adds `post:/api/users/authentication/google-signin = Limit=2/1m`; global `* = int.MaxValue`; unique spoofed IP. (Google verification will fail with a bad token — acceptable; we only assert non-429 then 429.)
- **Steps:**
  1. POST `/api/Users/Authentication/Google-SignIn` with `{IdToken:"invalid"}` twice. Record statuses (non-429).
  2. POST a 3rd time from the same IP.
- **Expected result:** Calls 1 & 2 → non-429 (handler/verification failure, e.g. 400/424/401). **Call 3 → 429.**
- **Traces to:** AC-1.

### BE-TC-06 — IP counter is independent across distinct client IPs (limit is per-IP, not global)
- **Type:** functional / boundary
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory rule `post:/api/users/authentication/sign-in = Limit=2/1m`; two distinct spoofed `X-Real-IP` values (IP-A, IP-B).
- **Steps:**
  1. From IP-A, POST Sign-In 3 times → 3rd is 429 (counter for A exhausted).
  2. From IP-B, POST Sign-In once.
- **Expected result:** IP-A's 3rd call → 429; **IP-B's first call → non-429** (its own counter is fresh). Confirms the limit is keyed per client IP, not a shared global counter.
- **Traces to:** AC-1 (per-IP semantics).

---

## Group B — Per-endpoint isolation (AC-1a)

### BE-TC-07 — One endpoint's burst does NOT throttle a different endpoint
- **Type:** functional / regression
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory rule constrains ONLY `post:/api/users/authentication/sign-in = Limit=2/1m`; global `* = int.MaxValue`; unique spoofed IP (same IP for both endpoints).
- **Steps:**
  1. Exhaust Sign-In from IP-X (3 calls → 3rd is 429).
  2. From the same IP-X, POST `/api/Users/Authentication/Register-Parent` (invalid body) 5 times.
- **Expected result:** Sign-In 3rd → 429; **all 5 Register-Parent calls → non-429 (422 each).** Proves `EnableEndpointRateLimiting=true` keeps per-endpoint counters separate (sign-in's limit doesn't bleed onto register-parent).
- **Traces to:** AC-1a. *(Extends existing AC-RL-4.)*

### BE-TC-08 — `EnableEndpointRateLimiting` is on (per-endpoint rules are honored, not folded into global)
- **Type:** functional (toggle guard)
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Default factory rules (no PostConfigure override of `EnableEndpointRateLimiting`); rely on the production-config path being `true`. Constrain two endpoints to `Limit=2/1m` each via PostConfigure, keeping `EnableEndpointRateLimiting=true`.
- **Steps:**
  1. Exhaust endpoint A (sign-in) to a 429 (3 calls).
  2. Independently exhaust endpoint B (forgot-password) to a 429 (3 calls), same IP.
- **Expected result:** **Both** endpoints reach their own 429 independently (each needs its own 3rd call). If `EnableEndpointRateLimiting` were `false`, the two endpoints would share the global counter and the 2nd endpoint would 429 earlier/inconsistently. Asserting both reach 429 only on their own 3rd call confirms per-endpoint accounting.
- **Traces to:** AC-1a.

---

## Group C — 429 response shape (AC-1b)

### BE-TC-09 — 429 response has a non-empty body (middleware active)
- **Type:** functional / state (error response)
- **Priority:** P1
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory rule `sign-in = Limit=2/1m`; unique spoofed IP.
- **Steps:**
  1. Exhaust Sign-In (2 calls), then make the 3rd.
  2. Read the 429 response body.
- **Expected result:** Status 429; **body is non-empty** (AspNetCoreRateLimit quota-exceeded message, e.g. "API calls quota exceeded! maximum admitted 2 per 1m."). An empty body would indicate the middleware isn't wired in `Program.cs`.
- **Traces to:** AC-1b. *(Maps to existing AC-RL-5.)*

### BE-TC-10 — 429 response standard rate-limit metadata (`Retry-After` / headers)
- **Type:** functional / contract
- **Priority:** P2 (informational — see README open question #4)
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory rule `sign-in = Limit=2/1m`; unique spoofed IP.
- **Steps:**
  1. Trip the 429 on the 3rd Sign-In call.
  2. Inspect response headers for `Retry-After` and/or `X-Rate-Limit-*`.
- **Expected result:** Document the actual behaviour. If `Retry-After` is present and parseable, assert it is > 0. If absent (current config does not customize `QuotaExceededResponse`), record as a finding for the lead (open question #4) — **do not fail the build** on its absence unless the lead confirms it's a requirement.
- **Traces to:** AC-1b (back-off UX support).

---

## Group D — Whitelist & non-regression (AC-1c / AC-1d)

### BE-TC-11 — Health probes are whitelisted (never throttled)
- **Type:** functional / regression
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Factory with any tiny rule active; `EndpointWhitelist=["get:/health","get:/health/live"]` (production parity).
- **Steps:**
  1. GET `/health` 10 times rapidly from one IP. Record statuses.
  2. GET `/health/live` 10 times rapidly. Record statuses.
- **Expected result:** **No 429** in either set; all 20 calls return 200. Confirms orchestrator liveness/readiness probes are never throttled.
- **Traces to:** AC-1c. *(Maps to existing AC-RL-3 / AC-RL-3b.)*

### BE-TC-14 — Valid sign-in under the limit still returns 200 + valid envelope (no collateral breakage)
- **Type:** functional / regression / persistence-of-behaviour
- **Priority:** P0
- **Target agent:** `api-tester`
- **Preconditions / seed:** Seeded superadmin (`UserSeeder.SeedSuperAdminAsync`); factory rule `sign-in = Limit=2/1m`; unique spoofed IP (only 1 call, well under limit).
- **Steps:**
  1. POST `/api/Users/Authentication/Sign-In` once with valid creds (`{UserName:"superadmin", Password:"123Pa$$word!"}`).
  2. Parse the response body.
- **Expected result:** **200 OK** (not 429); body is `BaseResponse<JwtAuthResponse>` with `Successed == true`, `data.accessToken` non-empty. Confirms rate limiting does not break the normal happy path; envelope spelling `Successed` intact.
- **Traces to:** AC-1d. *(Maps to existing AC-RL-6.)*

---

## Group E — Production-rule resolution & deferred items

### BE-TC-12 — Production/Staging environment resolves the TIGHTENED limits — **BLOCKED (needs Production test host)**
- **Type:** auth-authz / config (env-gating)
- **Priority:** P1
- **Target agent:** `api-tester`
- **Status:** **BLOCKED** — the standard `Testing`-env factory resolves the *loose* (100/1s) branch, and the deterministic factory overrides the rules entirely. Exercising the `Production`/`Staging` branch of `ConfigureRateLimitingOptions` requires a dedicated `WebApplicationFactory` with `UseEnvironment("Production")` plus the env secrets it then demands (`JwtSettings__Secret`, etc.) so the host boots. See README open question #3.
- **Preconditions / seed (if unblocked):** A `Production`-env factory (no PostConfigure override of `GeneralRules`), with all required prod env vars supplied so startup succeeds.
- **Steps (if unblocked):**
  1. Hit `/api/Users/Authentication/Forgot-Password` from one IP up to its Production limit (5/15m) → 6th call.
- **Expected result (if unblocked):** First 5 → generic 200; **6th → 429.** Confirms the tightened Production thresholds (forgot-password `5/15m`, register-parent `10/15m`, sign-in `50/5m`, etc.) resolve under `Production`/`Staging`. **If the lead deems env-gating covered by code review + the `GuardJwtSecret` precedent, mark this Won't-Test and note it.**
- **Traces to:** AC-1e.

### BE-TC-13 — Multi-instance (Redis-backed) shared counter — **NOT TESTABLE in P1-13b (deferred to P6-06-BE-4)**
- **Type:** scalability / availability
- **Priority:** P2 (documentation placeholder)
- **Target agent:** `api-tester`
- **Status:** **NOT TESTABLE — out of P1-13b scope.** The store is intentionally **in-memory** for single-instance in Phase 1; cross-replica counter sharing (Redis) is **P6-06-BE-4**. With an in-memory store, two instances each keep their own counter (a 2-replica deployment effectively doubles the per-IP allowance) — by design for now. This case exists only to record the known limitation and its tracking ID so it is not silently dropped.
- **Expected result:** Re-scope and implement under the **P6-06** QC run once the Redis store lands. No assertion here.
- **Traces to:** AC-1f (deferred), P6-06-BE-4.

---

## Out-of-scope for this run (relocated to P6-06 — documented, not designed here)
- **AC-2** Forgot-password timing-oracle decouple → P6-06-BE-1.
- **AC-3** Localized transactional emails (ar/en) → P6-06-BE-2.
- **AC-4** Transport/secret hygiene (Dev-gate `RequireHttpsMetadata`; DB password via env) → P6-06-BE-3.
These will be QC'd under a separate P6-06 test-case pass.
