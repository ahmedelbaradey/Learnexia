# QC Test Plan & Coverage Report — P1-13 (Backend hardening: lockout, sign-in safety, admin seed, CAPTCHA)

- **Story:** [P1-13 — Phase 1 backend hardening](../../../user-stories/Phase-1-Foundation/P1-13-backend-hardening.md)
- **Tasks:** [P1-13-BE](../../../tasks/Backend/Phase-1-Foundation/P1-13-BE.md) (BE-1 lockout, BE-2 sign-in safety, BE-3 admin seed, BE-4 CAPTCHA)
- **Run type:** **Backend-only** QC design pass (no frontend surface in scope).
- **Author:** qc-test-architect (design only — no test code, no execution).
- **Date:** 2026-06-07
- **Module under test:** Identity — `AuthenticationController` (`api/Users/Authentication/*`), `SignInCommandHandler`, `RegisterParentCommandHandler`, `UserSeeder`/`IdentitySeeder`, `TurnstileCaptchaVerifier`, `GuardCaptcha`.

---

## 1. Summary

P1-13 closes the security/operability gaps on the Phase-1 sign-in path and admin seed:

- **BE-1 — Lockout engaged.** `SignInCommandHandler` calls `CheckPasswordSignInAsync(..., lockoutOnFailure: true)`. Config: `MaxFailedAccessAttempts=5`, `DefaultLockoutTimeSpan=5 min`, `AllowedForNewUsers=true`. A successful sign-in resets the failed-count. Locked account → `400` with `LoginTooManyFailedAttempts` (en/ar localized).
- **BE-2 — Sign-in safety / anti-enumeration.** Not-found and wrong-password both return `400` + the **same** `LoginInvalidCredentials` message (previously not-found returned `404` — an enumeration oracle, now removed). On the not-found path a dummy `PasswordHasher.HashPassword` call burns equivalent CPU to mitigate a timing oracle. Exceptions never echo `ex.Message`; they log server-side and return generic `500` `LoginSystemError`. Deactivated account → `400` `LoginAccountDeactivated`.
- **BE-3 — Config/env admin seed.** `UserSeeder.SeedConfiguredAdminAsync` reads `AdminSeed:Email` + `AdminSeed:Password` (env `AdminSeed__Email` / `AdminSeed__Password`). No-ops when either is blank (no committed fallback). Idempotent (skips if email exists). Ensures the `Admin` role. Legacy `superadmin`/`basicuser` (committed password `123Pa$$word!`) are seeded **only in Development** (`IdentitySeeder` gates on `IsDevelopment()`); non-Development seeds only role/permission claims.
- **BE-4 — CAPTCHA on register.** `ICaptchaVerifier` (Cloudflare Turnstile) gates `Register-Parent`. Config-gated (`Captcha:Enabled`, default `false` → no-op pass-through), fail-closed when enabled (missing token / HTTP / parse error → `false`). Failure → `400` `CaptchaVerificationFailed`. `GuardCaptcha` fail-fasts at startup in Production/Staging when CAPTCHA is disabled or the secret is empty.

### Scope
- **In scope (HTTP/runtime, integration tests):** the sign-in path (lockout threshold + reset, anti-enumeration message/status parity, exception non-leakage, deactivated), the register CAPTCHA gate behavior, and the config/idempotency/no-op behavior of the admin seed where it is observable through the running app.
- **Out of scope:** frontend (no `frontend-test-cases.md`); the two startup-`Guard` fail-fast cases (`GuardCaptcha`, and the pre-existing `GuardJwtSecret`) are **design-flagged but partially blocked** — they require booting the host in `Production`/`Staging` with bad config, which the Testcontainers `WebApplicationFactory` (runs as `Testing`) does not exercise directly; see §4 + the blocked cases in `backend-test-cases.md`.
- **Timing-oracle mitigation (audit finding #1):** the dummy-hash on the not-found path is verified by **behavioral parity** (same status + message + reasonable latency), not by a brittle absolute-latency assertion. Documented as a non-deterministic risk in §3.

### Counts
- **Total cases:** 34 — all backend (`BE-TC-01`..`BE-TC-34`). No frontend cases.
- **By priority:** **P0 = 17**, **P1 = 12**, **P2 = 5**.
- **By area:** Lockout (BE-1) = 9 · Sign-in safety/anti-enumeration (BE-2) = 10 · Admin seed (BE-3) = 7 · CAPTCHA (BE-4) = 8.
- **Blocked / not-testable-yet:** 3 (`BE-TC-19` admin-seed idempotency on second boot, `BE-TC-33` GuardCaptcha prod fail-fast, `BE-TC-34` legacy-creds non-Development gate — all require a host boot under a non-Testing environment / second migration pass; see §4).

> **Note for `api-tester`:** A CAPTCHA integration suite **already exists** at `backend/tests/Learnexia.IntegrationTests/P1_13_BE4_Captcha_Tests.cs` (uses a `FakeCaptchaVerifier`). The CAPTCHA cases below (`BE-TC-26`..`BE-TC-32`) map 1:1 onto that file — **verify they exist and pass; do not duplicate**. The lockout (`BE-TC-01`..`BE-TC-09`) and anti-enumeration (`BE-TC-10`..`BE-TC-25`) cases are the **net-new** work for this run.

---

## 2. Coverage matrix (acceptance criterion → case IDs)

| # | Acceptance criterion (story §AC) | Case IDs | Covered? |
|---|----------------------------------|----------|----------|
| AC-1 | **Lockout engaged** after `MaxFailedAccessAttempts=5` consecutive failures; locked account returns clear localized (en/ar) `BaseResponse` | BE-TC-01, 02, 03, 04, 05, 06, 08, 09 | ✅ |
| AC-1b | **Counter resets on a successful sign-in** | BE-TC-07 | ✅ |
| AC-2 | **Sign-in safety — no raw exception text**: generic localized `ServerError`/`LoginSystemError`, detail logged server-side | BE-TC-20, 21 | ✅ |
| AC-2b | **Anti-enumeration**: wrong-email vs wrong-password **indistinguishable** (single "invalid credentials" result — same status + same message) | BE-TC-10, 11, 12, 13, 14, 15, 22, 23 | ✅ |
| AC-2c | **Timing parity** on the not-found path (dummy-hash mitigation, finding #1) | BE-TC-16 (behavioral) | ⚠️ partial (latency non-deterministic — behavioral parity only) |
| AC-3 | **Config-driven admin seed**: idempotent, env-sourced, **no committed credential**; admin role ensured | BE-TC-17, 18, 19 (blocked), 24, 25 | ✅ (BE-TC-19 blocked) |
| AC-3b | **Legacy `superadmin`/`basicuser` committed password removed/guarded out of non-Development** | BE-TC-25, BE-TC-34 (blocked) | ⚠️ partial (non-Dev boot blocked) |
| AC-4 | **Anti-automation on register**: pluggable CAPTCHA gate, config-gated (no-op in dev/tests), in addition to IP rate-limit | BE-TC-26, 27, 28, 29, 30, 31, 32 | ✅ (existing suite) |
| AC-4b | **CAPTCHA misconfig fail-fast** in Production/Staging (`GuardCaptcha`) | BE-TC-33 (blocked) | ⚠️ blocked (needs Prod/Staging boot) |
| AC-5 | Auth-path / secrets / admin-seed pass security-auditor (Critical/High block) | — | ✅ already PASS-WITH-FOLLOWUPS (both audits read; no Critical/High) |
| — | **Product override**: no Student/Teacher self-register via the parent register path (role server-assigned) | BE-TC-32 | ✅ |

**Gap verdict:** Every release-gating acceptance criterion has at least one P0/P1 case. Three cases are **blocked-by-environment** (not gaps in design): AC-3 idempotency-across-boots, AC-3b/AC-4b prod-only fail-fast guards, and the legacy-creds non-Development gate. These need either a host boot under `Production`/`Staging` with crafted config (outside the current Testing `WebApplicationFactory`) or a dedicated boot-time test fixture — see §4 open question Q3. **AC-2c** (timing oracle) is intentionally covered only behaviorally; an absolute-latency assertion would be flaky and is explicitly out.

---

## 3. Risk notes (where cases are weighted, and why)

1. **Lockout threshold boundary (highest weight).** The whole point of BE-1 is the off-by-one around attempt #5. Cases pin: attempts 1–4 stay `InvalidCredentials` (not locked), attempt **5 locks**, attempt 6+ stays locked, a **correct** password on a locked account is still rejected as locked (lockout takes precedence over a valid credential — this is the security-critical assertion), and a success **before** the threshold resets the counter so the next failed run starts fresh. A regression here silently disables brute-force protection.
2. **Anti-enumeration parity (highest weight).** The defect this story fixes was a `404` vs `400` oracle. Cases assert byte-for-byte parity: same HTTP status (`400`), same `Successed=false`, same message string for *not-found* and *wrong-password*. Any divergence (status, message, body shape, or even a different `errors` payload) re-opens the oracle. Includes a locale-parity check (en and ar each internally consistent across the two paths).
3. **Lockout interaction with anti-enumeration (subtle).** Audit finding #2: the locked-out message (`LoginTooManyFailedAttempts`) is only reachable for a **real** account, so triggering it leaks existence. This is an **accepted trade-off** — cases assert the *current* documented behavior (locked message differs from invalid-credentials), and a note flags it so a future "strict non-enumeration" decision doesn't get mistaken for a regression.
4. **Lockout-DoS (finding #3).** An attacker who knows a victim's email can lock them for 5 min with 5 bad passwords. Accepted; auto-expires. No test asserts the *absence* of this (it's by-design), but BE-TC-09 documents the 5-min auto-expiry expectation as the mitigating control.
5. **Admin-seed credential exposure.** The seed must never run with a committed credential and must no-op cleanly when unconfigured. Cases assert the no-op (blank config → no admin created, app still boots) and idempotency. The committed-credential-absence is also enforced by reading `appsettings.json` (`AdminSeed:Password == ""`) as a static assertion.
6. **CAPTCHA fail-open risk.** The verifier must fail **closed** when enabled. The existing suite drives a `FakeCaptchaVerifier`; the real `TurnstileCaptchaVerifier` fail-closed paths (timeout, non-2xx, malformed JSON, null token) are unit-level concerns the integration suite models via the fake returning `false`. The **production fail-fast guard** (`GuardCaptcha`) is the residual risk and is blocked (BE-TC-33).
7. **Non-determinism (timing).** AC-2c is the one place a naive test would be flaky. Cases deliberately avoid asserting an absolute latency delta; they assert behavioral parity only and leave a note.

---

## 4. Open questions / assumptions (lead must resolve before/around implementation)

- **Q1 — Lockout window & test isolation.** Lockout state is persisted on the `User` row (`AccessFailedCount`, `LockoutEnd`). The Testcontainers DB is shared across a test collection. **Assumption:** each lockout case registers its **own unique parent** (via `Register-Parent`) so failed-attempt counters don't bleed between cases. `AllowedForNewUsers=true` means freshly-registered users are lockable immediately. Confirm the tester seeds a per-case user rather than reusing `superadmin`/`basicuser`.
- **Q2 — Forcing the 500 path (BE-TC-20/21).** The generic `LoginSystemError` `500` only fires when `Handle` throws. There is no clean public hook to force an exception against the real handler. **Assumption:** the tester forces it via a test double (e.g. a `SignInManager`/`IIdentityServiceManager` stub that throws), mirroring how `FakeCaptchaVerifier` is injected in the CAPTCHA factory. If a throwing double is not feasible, downgrade BE-TC-20/21 to **code-review-verified** (the catch block is already audited as fixed) and note it in the execution report. Lead to confirm acceptable.
- **Q3 — Prod/Staging-only guards (BE-TC-19/33/34).** `GuardCaptcha`, the legacy-creds non-Development gate, and seed-idempotency-across-boots are only observable when the host boots under `Production`/`Staging` (or boots twice). The standard `LearnexiaWebAppFactory` runs as `Testing`. **Question:** does the lead want a dedicated boot-time fixture (a `WebApplicationFactory` with `UseEnvironment("Production")` + crafted config asserting the host throws / no legacy account exists), or are these accepted as **code-review-verified** (both security audits already confirm them)? Until decided, they are marked **blocked** here.
- **Q4 — `MaxFailedAccessAttempts` semantics.** Confirmed config is **5**; the boundary case assumes the account locks **on** the 5th consecutive failed attempt (ASP.NET Identity increments then checks `>= Max`). The tester should empirically confirm whether lock engages on attempt 5 or 6 in this Identity version and record the observed boundary in the execution report (BE-TC-04/05 are written to pin whichever it is, with attempt-5 as the expected lock point).

---

## 5. Handoff

| File | Goes to | Action |
|------|---------|--------|
| [`backend-test-cases.md`](./backend-test-cases.md) | **`api-tester`** | Implement `BE-TC-01`..`BE-TC-25` as net-new integration tests (`P1_13_BE1_Lockout_Tests.cs`, `P1_13_BE2_SignInSafety_Tests.cs`, `P1_13_BE3_AdminSeed_Tests.cs` suggested). For `BE-TC-26`..`BE-TC-32` **verify the existing `P1_13_BE4_Captcha_Tests.cs` covers them** (do not duplicate). Mark blocked cases per their stated blocker. |
| [`execution-report.md`](./execution-report.md) | **`api-tester`** (fills it) | After running, record pass/fail per case + any defects. The qc-test-architect created the empty template; the tester fills results — **the architect never fills results.** |

**Run facts (from HANDOFF.md):** integration suite is xUnit + Testcontainers PostgreSQL (`pgvector/pgvector:pg16`), `WebApplicationFactory<Program>` under `UseEnvironment("Testing")`, rate-limiting disabled in tests. Auth endpoints under `api/Users/Authentication/*`. Response envelope is `BaseResponse<T>` with the success flag spelled **`Successed`**; controller path serializes camelCase (Newtonsoft), the 422/middleware path serializes PascalCase (System.Text.Json) — use the existing `TryProp` case-insensitive helper. Set culture via the `Accept-Language` header (`en-US` / `ar-EG`) for localized-message assertions.
