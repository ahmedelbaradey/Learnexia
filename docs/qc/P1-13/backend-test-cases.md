# P1-13 — Backend Test Cases (for `api-tester`)

> **Target agent:** `api-tester` (integration tests, running API).
> **Harness:** xUnit + Testcontainers PostgreSQL via `WebApplicationFactory<Program>` (`Testing` env, rate-limit disabled). Mirror `LearnexiaWebAppFactory` / `CaptchaWebAppFactory`. Use the existing `TryProp` case-insensitive JSON helper (controller = camelCase/Newtonsoft, 422/middleware = PascalCase/System.Text.Json). Success flag is **`Successed`**.
> **Endpoints:** `POST api/Users/Authentication/Sign-In`, `POST api/Users/Authentication/Register-Parent`.
> **Localized keys (en-US / ar-EG):**
> - `LoginInvalidCredentials` — "Invalid username or password." / "اسم المستخدم أو كلمة المرور غير صحيحة."
> - `LoginTooManyFailedAttempts` — "Account temporarily locked…" / "تم قفل الحساب مؤقتاً…"
> - `LoginAccountDeactivated` — "Your account is inactive…" / "حسابك غير نشط…"
> - `LoginSystemError` — "An error occurred during sign-in…" / "حدث خطأ أثناء تسجيل الدخول…"
> - `CaptchaVerificationFailed` — "CAPTCHA verification failed…" / "فشل التحقق من اختبار CAPTCHA…"
> **Config under test:** `MaxFailedAccessAttempts=5`, `DefaultLockoutTimeSpan=5min`, `AllowedForNewUsers=true`. `AdminSeed:Email`/`:Password` (env `AdminSeed__*`). `Captcha:Enabled=false` (default).
>
> **General preconditions for sign-in cases:** unless stated, register a fresh, unique parent via `Register-Parent` (CAPTCHA disabled / fake=true) with a known password, then exercise sign-in against that account so lockout counters never bleed across cases.

---

## Area A — Account lockout (BE-1)

### BE-TC-01 — Wrong password below threshold returns invalid-credentials, not locked
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock01@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. `POST Sign-In` with `{ UserName: lock01@test.test, Password: "Wrong#1" }`.
- **Expected:** `400`; `Successed=false`; `message == LoginInvalidCredentials` (en). **Not** the locked message.
- **Traces to:** AC-1.

### BE-TC-02 — Each failed attempt below threshold stays invalid-credentials (attempts 1–4)
- **Type:** boundary · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock02@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. `POST Sign-In` with a wrong password 4 times in a row.
  2. After each, inspect the response.
- **Expected:** all 4 responses are `400` + `LoginInvalidCredentials`; **none** returns `LoginTooManyFailedAttempts`. Account is still usable with the correct password (verify in BE-TC-07).
- **Traces to:** AC-1.

### BE-TC-03 — Correct password still works while under the threshold
- **Type:** functional · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock03@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. 3 wrong-password sign-ins.
  2. 1 sign-in with the **correct** password.
- **Expected:** step-2 → `200`, `Successed=true`, non-empty `data.accessToken`. (Confirms not-yet-locked accounts authenticate.)
- **Traces to:** AC-1, AC-1b.

### BE-TC-04 — Threshold reached: 5th consecutive failure locks the account
- **Type:** boundary (security-critical) · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock04@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. `POST Sign-In` with a wrong password 5 consecutive times.
  2. Inspect the **5th** response (and if Identity locks on the 6th in this version, the 6th — record the observed boundary).
- **Expected:** by the 5th failed attempt the account is locked → `400` + `LoginTooManyFailedAttempts`. Record the exact attempt number at which the message flips from `LoginInvalidCredentials` to `LoginTooManyFailedAttempts` (per Q4).
- **Traces to:** AC-1.

### BE-TC-05 — Locked account rejects even the CORRECT password
- **Type:** negative (security-critical) · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock05@test.test` / `Str0ng@Pass`; lock it via 5 wrong-password attempts.
- **Steps:**
  1. After lockout, `POST Sign-In` with the **correct** password.
- **Expected:** `400` + `LoginTooManyFailedAttempts` (lockout takes precedence over a valid credential — `IsLockedOut` is checked before `Succeeded`). **Not** `200`, **not** `LoginInvalidCredentials`.
- **Traces to:** AC-1.

### BE-TC-06 — Attempts beyond the threshold stay locked
- **Type:** boundary · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock06@test.test`; lock it (5 failures).
- **Steps:**
  1. 2 more wrong-password sign-ins after lockout.
- **Expected:** both `400` + `LoginTooManyFailedAttempts` (still locked; counter doesn't "overflow" into a different result).
- **Traces to:** AC-1.

### BE-TC-07 — Successful sign-in resets the failed-attempt counter
- **Type:** functional (security-critical) · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock07@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. 4 wrong-password sign-ins (one below threshold).
  2. 1 correct-password sign-in → expect `200`.
  3. 4 more wrong-password sign-ins.
  4. Inspect the 4th response in step 3.
- **Expected:** step-2 succeeds; step-3's 4th attempt still returns `LoginInvalidCredentials` (**not** locked) — proving the success in step 2 reset the counter to 0 (otherwise 4+4=8 would have locked at the 5th overall).
- **Traces to:** AC-1b.

### BE-TC-08 — Locked-account message is localized in Arabic
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock08@test.test`; lock it (5 failures, send `Accept-Language: ar-EG` on the lock-triggering call).
- **Steps:**
  1. With header `Accept-Language: ar-EG`, `POST Sign-In` with a wrong password against the locked account.
- **Expected:** `400`; `message` equals the **Arabic** `LoginTooManyFailedAttempts` value ("تم قفل الحساب مؤقتاً…"), not the English string.
- **Traces to:** AC-1 (localized en/ar).

### BE-TC-09 — Lockout auto-expiry documented (5-min window)
- **Type:** functional (documentation/observation) · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `lock09@test.test`; lock it.
- **Steps:**
  1. Confirm immediately-after lockout → `LoginTooManyFailedAttempts`.
  2. *(Optional / time-boxed — do not block on a 5-min wait in CI.)* If feasible with a controllable clock, advance past `DefaultLockoutTimeSpan` and confirm the correct password authenticates again.
- **Expected:** locked while inside the 5-min window; auto-unlocks after it. If a 5-min real wait is infeasible, mark this **observation-only / not-run-in-CI** and record the design expectation (mitigates lockout-DoS per audit finding #3).
- **Traces to:** AC-1 (5-min window / DoS mitigation).

---

## Area B — Sign-in safety & anti-enumeration (BE-2)

### BE-TC-10 — Non-existent user returns 400 invalid-credentials (not 404)
- **Type:** auth-authz / negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** none (use a guaranteed-absent email like `nobody_<guid>@nope.test`).
- **Steps:**
  1. `POST Sign-In` with `{ UserName: <absent email>, Password: "Whatever#1" }`.
- **Expected:** `400` (**not** `404`); `Successed=false`; `message == LoginInvalidCredentials`.
- **Traces to:** AC-2b.

### BE-TC-11 — Existing user + wrong password returns 400 invalid-credentials
- **Type:** negative · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum11@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. `POST Sign-In` with the right email and a wrong password (single attempt, below lockout threshold).
- **Expected:** `400`; `Successed=false`; `message == LoginInvalidCredentials`.
- **Traces to:** AC-2b.

### BE-TC-12 — Not-found and wrong-password are byte-for-byte indistinguishable
- **Type:** auth-authz (security-critical) · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum12@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. Capture response A: sign-in with an **absent** email.
  2. Capture response B: sign-in with `enum12@test.test` + a wrong password.
  3. Compare: HTTP status, `Successed`, `statusCode` field, `message`, and the presence/shape of `errors`/`data`.
- **Expected:** A and B are **identical** on status (`400`), `Successed` (`false`), `statusCode` (`400`), `message` (`LoginInvalidCredentials`), and body shape. No field distinguishes a registered email from an unregistered one.
- **Traces to:** AC-2b.

### BE-TC-13 — Anti-enumeration parity holds in English
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum13@test.test`.
- **Steps:**
  1. With `Accept-Language: en-US`, capture not-found and wrong-password responses.
- **Expected:** both return the **same** English `LoginInvalidCredentials` string ("Invalid username or password.").
- **Traces to:** AC-2b.

### BE-TC-14 — Anti-enumeration parity holds in Arabic
- **Type:** RTL-i18n · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum14@test.test`.
- **Steps:**
  1. With `Accept-Language: ar-EG`, capture not-found and wrong-password responses.
- **Expected:** both return the **same** Arabic `LoginInvalidCredentials` string ("اسم المستخدم أو كلمة المرور غير صحيحة."). The two paths must not differ in localized text either.
- **Traces to:** AC-2b.

### BE-TC-15 — Deactivated account returns its own clear message (distinct from invalid-credentials)
- **Type:** functional · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** an account with `IsActive=false`. Seed via DB/manager in the fixture (no API toggles `IsActive`).
- **Steps:**
  1. `POST Sign-In` with the deactivated account's **correct** credentials.
- **Expected:** `400`; `message == LoginAccountDeactivated`. (This is checked **before** the password check, so it precedes lockout/invalid-credentials.)
- **Traces to:** AC-2 (deactivated path), with the note that this is a known, accepted existence signal for inactive accounts.

### BE-TC-16 — Not-found path performs comparable work (timing-oracle mitigation, behavioral)
- **Type:** auth-authz (behavioral) · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum16@test.test`.
- **Steps:**
  1. Issue many not-found sign-ins and many wrong-password sign-ins; observe responses.
- **Expected:** both paths return identical `400`/`LoginInvalidCredentials`. **Do NOT** assert an absolute latency delta (flaky). This case exists to document the dummy-hash mitigation (finding #1) and to assert behavioral parity only. If the tester wants a non-blocking timing sanity check, gate it behind a wide tolerance and mark it informational.
- **Traces to:** AC-2c (timing parity — behavioral only).

### BE-TC-17 — Sign-in success returns a well-formed BaseResponse envelope
- **Type:** functional / persistence · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum17@test.test` / `Str0ng@Pass`.
- **Steps:**
  1. Sign in with correct credentials.
- **Expected:** `200`; `Successed=true`; `statusCode==200`; `data.accessToken` non-empty; `data.userId > 0`; `message` present; `errors` present. Confirms the hardened handler didn't regress the happy path.
- **Traces to:** AC-2 (no happy-path regression).

### BE-TC-18 — Missing UserName/Password → 422 validation (not 400/500)
- **Type:** validation · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** none.
- **Steps:**
  1. `POST Sign-In` with `{ }` (empty body) and separately with only one of the two fields.
- **Expected:** `422` (`ValidationBehavior` runs for `ICommand`) with `Errors[]`; `Successed=false`. Confirms validation precedes the handler and doesn't leak via the 500 path. *(If `SignInCommand` has no validator, record the actual status — likely model-binding `400` — and flag whether a validator is expected.)*
- **Traces to:** AC-2 (no leakage; correct status mapping).

### BE-TC-19 — *(BLOCKED)* Admin-seed idempotency across two host boots
- See Area C / BE-TC-19 below — listed there. *(ID reserved; cross-referenced.)*

### BE-TC-20 — Internal exception returns generic 500, never raw `ex.Message`
- **Type:** negative (security) · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** a `WebApplicationFactory` that injects a throwing double for the sign-in dependency chain (e.g. `IIdentityServiceManager`/`SignInManager` stub that throws inside `Handle`), per Q2.
- **Steps:**
  1. `POST Sign-In` against an account whose handler path is forced to throw.
- **Expected:** `500`; `message == LoginSystemError` (generic, localized); body contains **no** stack trace, no "Exception", no "   at ", no DB/Identity internals. (If a throwing double is infeasible, downgrade to **code-review-verified** and note it.)
- **Traces to:** AC-2.

### BE-TC-21 — Exception detail is logged server-side, not returned
- **Type:** negative (security) · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** same throwing-double fixture as BE-TC-20, with a capturing `ILoggerManager`.
- **Steps:**
  1. Force the exception; inspect the captured log + the HTTP body.
- **Expected:** the exception detail appears in the server log (`Error: in SignInCommand`) but **not** in the response body. (Downgrade to code-review-verified if log capture is impractical.)
- **Traces to:** AC-2.

### BE-TC-22 — Lockout message is distinct from invalid-credentials (accepted enumeration trade-off, regression pin)
- **Type:** regression · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum22@test.test`; lock it.
- **Steps:**
  1. Compare the locked-account response message vs an invalid-credentials response message.
- **Expected:** they **differ** (`LoginTooManyFailedAttempts` vs `LoginInvalidCredentials`). This pins audit finding #2's accepted trade-off so a future "strict non-enumeration" change is a deliberate decision, not a silent regression. Add a comment referencing finding #2.
- **Traces to:** AC-2b (documented trade-off).

### BE-TC-23 — Email case-insensitivity does not create an enumeration signal
- **Type:** negative · **Priority:** P2 · **Agent:** api-tester
- **Preconditions/seed:** fresh parent `enum23@test.test`.
- **Steps:**
  1. Sign in with `ENUM23@TEST.TEST` (uppercased) + wrong password.
  2. Sign in with `enum23@test.test` (exact) + wrong password.
- **Expected:** both `400` + `LoginInvalidCredentials` — normalization must not turn a known-email-wrong-case into a distinguishable result vs an absent email.
- **Traces to:** AC-2b.

---

## Area C — Config-driven admin seed (BE-3)

### BE-TC-24 — `AdminSeed` blank in committed config → no admin created, app boots
- **Type:** functional / persistence · **Priority:** P1 · **Agent:** api-tester
- **Preconditions/seed:** default `Testing` factory (committed `appsettings.json` has `AdminSeed:Email=""`, `:Password=""`).
- **Steps:**
  1. Boot the app + seed; then attempt `Sign-In` with any plausible admin email — and assert no account was created from the blank config.
- **Expected:** app boots and seeds without error; **no** admin account exists for an empty-config seed (no committed fallback credential). Confirms `SeedConfiguredAdminAsync` no-ops when email/password blank.
- **Traces to:** AC-3 (no committed credential / no-op when unconfigured).

### BE-TC-25 — Configured `AdminSeed` creates an admin that can sign in; legacy dev accounts present only in Development
- **Type:** functional / auth-authz · **Priority:** P0 · **Agent:** api-tester
- **Preconditions/seed:** a factory that injects `AdminSeed:Email=admin@learnexia.test` + `AdminSeed:Password=Str0ng@Adm1n!` via in-memory config (mirror the `ConfigureAppConfiguration` override used for the connection string).
- **Steps:**
  1. Boot + seed; `POST Sign-In` with the configured admin creds.
  2. Assert the resulting token carries the `Admin` role (decode JWT or call an admin-gated endpoint if one exists).
  3. In the `Testing`/Development factory, confirm `superadmin`/`basicuser` still sign in with `123Pa$$word!` (existing CAPTCHA-suite regression already asserts this).
- **Expected:** configured admin signs in (`200`, `Admin` role present); the `Admin` role exists; legacy accounts work **only** because the test env behaves as Development for seeding. Records that the committed password is dev-only.
- **Traces to:** AC-3, AC-3b.

### BE-TC-19 — *(BLOCKED)* Admin-seed idempotency across two boots / re-seed
- **Type:** persistence (idempotency) · **Priority:** P1 · **Agent:** api-tester · **STATUS: BLOCKED**
- **Blocker:** requires running the seed twice against the same DB (second host boot or a second `SeedAsync` call) to prove `SeedConfiguredAdminAsync` skips an already-existing admin and creates no duplicate. The standard single-boot factory does not exercise a re-seed.
- **Intended steps (when unblocked):**
  1. Boot+seed with a configured admin; capture the admin user id.
  2. Re-run `IdentitySeeder.SeedAsync` (or boot a second factory on the same container).
  3. Query the admin user(s) by email.
- **Expected:** exactly one admin account; no exception; no duplicate role assignment. Until a re-seed fixture exists (Q3), mark **blocked** and optionally cover by directly invoking `SeedConfiguredAdminAsync` twice in a fixture.
- **Traces to:** AC-3 (idempotent).

### BE-TC-34 — *(BLOCKED)* Legacy committed-password accounts are NOT created in non-Development
- **Type:** auth-authz (security) · **Priority:** P1 · **Agent:** api-tester · **STATUS: BLOCKED**
- **Blocker:** the `IsDevelopment()` gate only skips `superadmin`/`basicuser` seeding when the host runs as a non-Development environment. The Testcontainers factory runs as `Testing`, which the seed treats as Development-like for the legacy path. Requires a `UseEnvironment("Production")` (or `Staging`) boot fixture.
- **Intended steps (when unblocked):**
  1. Boot the host as `Production` with a valid JWT secret + CAPTCHA enabled + admin config (so the guards pass).
  2. Attempt `Sign-In` with `superadmin` / `123Pa$$word!`.
- **Expected:** sign-in **fails** (`400` invalid-credentials) — the legacy account with the committed password was never seeded outside Development; only role/permission claims were seeded. Until a Production boot fixture exists (Q3), mark **blocked**; both security audits already verify this by code review.
- **Traces to:** AC-3b.

---

## Area D — CAPTCHA on register (BE-4) — verify the existing suite

> The cases below **map onto the existing** `backend/tests/Learnexia.IntegrationTests/P1_13_BE4_Captcha_Tests.cs`. `api-tester`: confirm each is covered and green; do **not** duplicate. Add only what's missing (BE-TC-33 is genuinely missing and blocked).

### BE-TC-26 — CAPTCHA disabled (default) → register succeeds with no token
- **Type:** functional (regression) · **Priority:** P0 · **Agent:** api-tester
- **Steps:** Register-Parent with valid body and **no** `captchaToken`, verifier no-op (`Enabled=false` / fake=true).
- **Expected:** `200`; `Successed=true`; non-empty `accessToken`; `isFirstLogin=true`. *(Existing: AC-DEF-1..4.)*
- **Traces to:** AC-4 (no-op in dev/tests).

### BE-TC-27 — CAPTCHA enabled + verification fails → 400, no account created
- **Type:** negative (security) · **Priority:** P0 · **Agent:** api-tester
- **Steps:** fake=false; Register-Parent with a token; then attempt sign-in with that email.
- **Expected:** register `400` + `CaptchaVerificationFailed`; `Successed=false`; subsequent sign-in fails (no account persisted). *(Existing: AC-FAIL-1..4.)*
- **Traces to:** AC-4.

### BE-TC-28 — CAPTCHA enabled + verification passes → 200, account created & retrievable
- **Type:** functional / persistence · **Priority:** P0 · **Agent:** api-tester
- **Steps:** fake=true; Register-Parent with a token; confirm via sign-in and `GET /Me`.
- **Expected:** `200`; account created; `/Me` returns the user (`id > 0`). *(Existing: AC-PASS-1..4.)*
- **Traces to:** AC-4.

### BE-TC-29 — Missing/null token with CAPTCHA failing → 400 (fail-closed)
- **Type:** boundary (security) · **Priority:** P1 · **Agent:** api-tester
- **Steps:** fake=false; Register-Parent with **no** token.
- **Expected:** `400` + `Successed=false`; no account created. *(Existing: AC-NULL-1..2.)*
- **Traces to:** AC-4 (fail-closed).

### BE-TC-30 — CAPTCHA failure does not leak internals
- **Type:** negative (security) · **Priority:** P1 · **Agent:** api-tester
- **Steps:** fake=false; inspect the `400` body.
- **Expected:** generic localized `CaptchaVerificationFailed`; no "Exception"/stack trace/provider internals. *(Existing: AC-FAIL-5.)*
- **Traces to:** AC-4.

### BE-TC-31 — Validation (422) precedes the CAPTCHA check
- **Type:** validation · **Priority:** P1 · **Agent:** api-tester
- **Steps:** `AcceptedTerms=false` (and separately, invalid email) with fake=false.
- **Expected:** `422` from `ValidationBehavior` **before** the handler's CAPTCHA check; `Successed=false`; `Errors[]` present. *(Existing: Regression terms-false / invalid-email.)*
- **Traces to:** AC-4 (gate ordering).

### BE-TC-32 — Register-Parent cannot inject a role (product override: no Student/Teacher self-register)
- **Type:** auth-authz (product override) · **Priority:** P1 · **Agent:** api-tester
- **Steps:** Register-Parent with fake=true; attempt over-post of a `Role`/`Roles`/`IsActive` field in the JSON body; then inspect the created account's role.
- **Expected:** registration succeeds (extra fields ignored — `RegisterParentCommand` has no role field); the account is assigned **only** `Parent` (server-assigned). No Student/Teacher/Admin can be created via this anonymous path. *(Augments the existing suite — add if not present.)*
- **Traces to:** AC-4 / product override (no teacher role; parent-driven onboarding).

### BE-TC-33 — *(BLOCKED)* CAPTCHA misconfig fail-fast in Production/Staging (`GuardCaptcha`)
- **Type:** auth-authz / config (security) · **Priority:** P1 · **Agent:** api-tester · **STATUS: BLOCKED**
- **Blocker:** `GuardCaptcha` throws at **startup** only under a protected environment (`Production`/`Staging`) when `Captcha:Enabled=false` or `SecretKey` empty. The standard factory boots as `Testing`, where the guard only rejects the inconsistent "enabled-without-secret" case. Requires a `UseEnvironment("Production")` boot fixture asserting the host throws `InvalidOperationException`.
- **Intended steps (when unblocked):**
  1. Build a `WebApplicationFactory` with `UseEnvironment("Production")`, a valid JWT secret, and `Captcha:Enabled=false`.
  2. Attempt to start the host.
- **Expected:** host startup **throws** `InvalidOperationException` ("CAPTCHA must be enabled with a configured Captcha:SecretKey…"). Also assert the inconsistent case (`Enabled=true`, empty secret) throws even outside Production. Until a Production boot fixture exists (Q3), mark **blocked**; the captcha audit verifies this by code review.
- **Traces to:** AC-4b (fail-fast guard).

---

## Coverage summary
- **P0 (17):** BE-TC-01,02,03,04,05,07,10,11,12,17(P1→listed P0? no),20,25,26,27,28 … (authoritative per-case priority is in each block above).
- **Blocked (3):** BE-TC-19, BE-TC-33, BE-TC-34 — all gated on a non-`Testing` boot / re-seed fixture (open question Q3).
- **Existing-suite verification (7):** BE-TC-26..BE-TC-32 map to `P1_13_BE4_Captcha_Tests.cs`.
- **Net-new implementation (15):** BE-TC-01..BE-TC-18, BE-TC-22, BE-TC-23, BE-TC-24, BE-TC-25 (minus the blocked ones).
