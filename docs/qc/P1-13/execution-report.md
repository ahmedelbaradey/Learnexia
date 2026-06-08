# P1-13 — Execution Report

> **Owner of results:** `api-tester`.
> Record one row per `BE-TC-*` case from [`backend-test-cases.md`](./backend-test-cases.md). Status ∈ `PASS` / `FAIL` / `BLOCKED` / `SKIPPED` / `CODE-REVIEW-VERIFIED`.

- **Run date:** 2026-06-07
- **Run by:** api-tester (Claude Sonnet 4.6)
- **Harness:** xUnit + Testcontainers PostgreSQL (`pgvector/pgvector:pg16`), `WebApplicationFactory<Program>` (`Testing` env), rate-limiting disabled.
- **Suite files:**
  - `backend/tests/Learnexia.IntegrationTests/P1_13_BE1_Lockout_Tests.cs` (new)
  - `backend/tests/Learnexia.IntegrationTests/P1_13_BE2_SignInSafety_Tests.cs` (new)
  - `backend/tests/Learnexia.IntegrationTests/P1_13_BE3_AdminSeed_Tests.cs` (new — includes `AdminSeedWebAppFactory`)
  - `backend/tests/Learnexia.IntegrationTests/P1_13_BE4_Captcha_Tests.cs` (existing + BE-TC-32 added)
- **Command:**
  ```
  dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj \
    --filter "FullyQualifiedName~P1_13_BE1_Lockout|FullyQualifiedName~P1_13_BE2_SignIn|FullyQualifiedName~P1_13_BE3|FullyQualifiedName~P1_13_BE4_Captcha"
  ```
- **Overall result:** **48 passed / 0 failed / 4 skipped of 52** (34 specified cases: 27 PASS, 2 CODE-REVIEW-VERIFIED/SKIP, 3 BLOCKED/SKIP; plus 19 existing CAPTCHA sub-cases all green)

---

## Results

### Area A — Lockout (BE-1)

| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-01 | Wrong password below threshold = invalid-credentials | **PASS** | `BeTc01_WrongPasswordBelowThreshold_Returns400InvalidCredentials` | en-US header required (server defaults to ar-EG) |
| BE-TC-02 | Attempts 1–4 stay invalid-credentials | **PASS** | `BeTc02_Attempts1Through4_StayInvalidCredentials_NeverLocked` | |
| BE-TC-03 | Correct password works under threshold | **PASS** | `BeTc03_CorrectPasswordWorksWhileBelowThreshold` | |
| BE-TC-04 | 5th failure locks (record observed boundary) | **PASS** | `BeTc04_FifthConsecutiveFailure_LocksAccount` | **Observed lock boundary = attempt 5** (ASP.NET Identity: increment then check >= Max; locks on the 5th wrong-password attempt exactly) |
| BE-TC-05 | Locked account rejects correct password | **PASS** | `BeTc05_LockedAccount_RejectsCorrectPassword` | `IsLockedOut` checked before `Succeeded` — lockout takes precedence |
| BE-TC-06 | Beyond-threshold attempts stay locked | **PASS** | `BeTc06_AttemptsAfterLockout_StayLocked` | |
| BE-TC-07 | Success resets the failed-attempt counter | **PASS** | `BeTc07_SuccessfulSignInResetsCounter` | Counter proved reset: 4+success+4 never triggers lockout |
| BE-TC-08 | Locked message localized (ar) | **PASS** | `BeTc08_LockedMessage_IsLocalizedInArabic` | Arabic string: "تم قفل الحساب مؤقتاً بسبب محاولات تسجيل دخول فاشلة كثيرة. يرجى المحاولة لاحقاً." |
| BE-TC-09 | 5-min auto-expiry (observation) | **PASS** (observation) | `BeTc09_LockoutAutoExpiry_ObservationOnly` | Asserts locked-immediately half only; auto-expiry not asserted in CI (5-min wait infeasible). Design expectation documented. |

### Area B — Sign-in safety & anti-enumeration (BE-2)

| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-10 | Non-existent user → 400 (not 404) | **PASS** | `BeTc10_NonExistentUser_Returns400_NotFound` | |
| BE-TC-11 | Existing user + wrong password → 400 | **PASS** | `BeTc11_ExistingUser_WrongPassword_Returns400InvalidCredentials` | |
| BE-TC-12 | Not-found vs wrong-password indistinguishable | **PASS** | `BeTc12_NotFound_And_WrongPassword_AreIndistinguishable` | Status, successed, statusCode, message, errors/data shape — all identical |
| BE-TC-13 | Anti-enumeration parity (en) | **PASS** | `BeTc13_AntiEnumerationParity_English` | Both paths return "Invalid username or password." |
| BE-TC-14 | Anti-enumeration parity (ar) | **PASS** | `BeTc14_AntiEnumerationParity_Arabic` | Both paths return "اسم المستخدم أو كلمة المرور غير صحيحة." |
| BE-TC-15 | Deactivated account → LoginAccountDeactivated | **PASS** | `BeTc15_DeactivatedAccount_ReturnsLoginAccountDeactivated` | Seeded directly via `UserManager` (no HTTP toggle endpoint exists); `IsActive=false` + correct credentials → 400 LoginAccountDeactivated |
| BE-TC-16 | Timing-oracle behavioral parity | **PASS** (behavioral) | `BeTc16_TimingOracleMitigation_BehavioralParity` | Asserts behavioral parity only (same 400 + same message). Absolute latency NOT asserted (flaky per spec). Dummy-hash code-review confirmed. |
| BE-TC-17 | Success envelope well-formed | **PASS** | `BeTc17_SuccessfulSignIn_ReturnsWellFormedEnvelope` | 200; successed=true; statusCode=200; message present; errors present; data.accessToken non-empty; data.userId > 0 |
| BE-TC-18 | Missing fields → 422 (or record actual) | **PASS** | `BeTc18a/b/c_...` | actual status: **422** — `SignInValidatior` via `ValidationBehavior` fires correctly for empty body, missing Password, and missing UserName |
| BE-TC-20 | Exception → generic 500, no ex.Message | **CODE-REVIEW-VERIFIED** | `BeTc20_...` (SKIP) | Forcing the 500 path requires a throwing double for `IIdentityServiceManager`/`SignInManager`. Catch block audited: logs `ex` server-side; returns generic `LoginSystemError` (no stack trace, no `ex.Message`). See Q2. |
| BE-TC-21 | Exception detail logged not returned | **CODE-REVIEW-VERIFIED** | `BeTc21_...` (SKIP) | Same blocker as BE-TC-20. `_logger.LogError(ex, "Error: in SignInCommand")` confirmed at `SignInCommandHandler.cs:105`. |
| BE-TC-22 | Locked message distinct from invalid-creds (trade-off pin) | **PASS** | `BeTc22_LockoutMessage_IsDistinctFrom_InvalidCredentials` | Confirmed distinct. Pins audit finding #2 accepted trade-off. |
| BE-TC-23 | Email case-insensitivity no enum signal | **PASS** | `BeTc23_EmailCaseInsensitivity_NoEnumerationSignal` | Both UPPERCASE and exact-case email + wrong password return identical 400 + LoginInvalidCredentials |

### Area C — Admin seed (BE-3)

| Case | Title | Status | Test method | Notes / defect |
|------|-------|--------|-------------|----------------|
| BE-TC-24 | Blank AdminSeed → no admin, app boots | **PASS** | `BeTc24_BlankAdminSeed_NoAdminCreated_AppBoots` | Probed 4 plausible admin emails; all return 400. Legacy `superadmin` still signs in. |
| BE-TC-25 | Configured admin signs in; legacy dev-only | **PASS** | `BeTc25a/b/c/d_...` (4 sub-tests) | `AdminSeedWebAppFactory` injects `AdminSeed:Email=admin-seed-test@learnexia.test` + `AdminSeed:Password=Str0ng@Adm1n!` via in-memory config. Admin signs in (200), JWT carries "Admin" role, GET /Me confirms "Admin" in roles. Legacy accounts still work. |
| BE-TC-19 | Idempotency across boots/re-seed (BLOCKED) | **BLOCKED** | `BeTc19_AdminSeedIdempotency_Blocked` (SKIP) | Needs a second `SeedAsync` call / second host boot on same container. Single-boot factory does not exercise re-seed. See Q3. |
| BE-TC-34 | Legacy creds NOT seeded in non-Development (BLOCKED) | **BLOCKED** | `BeTc34_LegacyCredsNotSeededNonDevelopment_Blocked` (SKIP) | Needs `UseEnvironment("Production")` boot fixture. Testing env explicitly seeds legacy accounts. Security audits verify by code review. See Q3. |

### Area D — CAPTCHA on register (BE-4) — verify existing suite

| Case | Title | Status | Test method (existing + new) | Notes / defect |
|------|-------|--------|------------------------|----------------|
| BE-TC-26 | Disabled default → register w/o token = 200 | **PASS** | `ACDef1/2/3/4_...` | 4 sub-cases all green |
| BE-TC-27 | Enabled + fail → 400, no account | **PASS** | `ACFail1/2/3/4/5_...` | 5 sub-cases all green |
| BE-TC-28 | Enabled + pass → 200, account retrievable | **PASS** | `ACPass1/2/3/4_...` | 4 sub-cases all green |
| BE-TC-29 | Null token fail-closed → 400 | **PASS** | `ACNull1/2_...` | 2 sub-cases all green |
| BE-TC-30 | Failure no internal leak | **PASS** | `ACFail5_...` | No "Exception" or "   at " in body |
| BE-TC-31 | Validation 422 precedes CAPTCHA | **PASS** | `Regression_AcceptedTermsFalse/InvalidEmail` | 2 regression tests all green |
| BE-TC-32 | No role injection (Parent only) | **PASS** | `BeTc32_RegisterParent_IgnoresRoleOverPost_OnlyParentRoleAssigned` | **ADDED** to P1_13_BE4_Captcha_Tests.cs. Over-posted Role/Roles/IsActive fields ignored; account gets only "Parent" role. |
| BE-TC-33 | GuardCaptcha prod fail-fast (BLOCKED) | **BLOCKED** | (not in existing suite) | Needs `UseEnvironment("Production")` boot fixture. CAPTCHA misconfig guard is code-review verified. See Q3. |

---

## Observed lockout boundary (Q4)

**Lock occurs on attempt 5 (not attempt 6).**

ASP.NET Identity's `CheckPasswordSignInAsync(..., lockoutOnFailure: true)` path:
1. Calls `IncrementAccessFailedCountAsync` → increments from 4 to 5.
2. The post-increment check `>= MaxFailedAccessAttempts (5)` triggers `SetLockoutEndDateAsync`.
3. Returns `SignInResult.LockedOut`.

Result: with `MaxFailedAccessAttempts=5`, the 5th consecutive wrong-password attempt returns `LoginTooManyFailedAttempts`. Attempts 1–4 return `LoginInvalidCredentials`. Confirmed by BE-TC-04.

---

## Defects found

None. All testable cases PASS. The implementation correctly:
- Locks on attempt 5 (boundary correct per config)
- Rejects correct password on locked account (lockout takes precedence)
- Resets counter on successful sign-in
- Returns byte-identical responses for not-found and wrong-password (anti-enumeration fix confirmed)
- Returns Arabic localization correctly
- Returns 422 for missing validation fields (not 400/500)
- Treats deactivated account separately with `LoginAccountDeactivated`
- Seeds admin from config with no committed credential; no-ops when config blank
- BE-TC-18c display name had a minor UTF encoding artifact in the source file → fixed to ASCII arrow

---

## Open questions raised during execution

**Q2 — Forcing the 500 path (BE-TC-20/21):**
Downgraded to CODE-REVIEW-VERIFIED. `SignInCommandHandler.Handle`'s catch block (lines 101-106) is already audited: logs exception detail via `ILoggerManager.LogError`, returns generic `LoginSystemError` (no `ex.Message`, no stack trace). To implement as running tests, a throwing stub for `IIdentityServiceManager` or `SignInManager<User>` would need to be injected via a custom `WebApplicationFactory` — feasible but requires the lead's approval (see Q2 in README). Recommended: approve the throwing-double fixture and implement after P1-13 merges.

**Q3 — Production boot fixtures (BE-TC-19, BE-TC-33, BE-TC-34):**
Three cases remain BLOCKED pending a `UseEnvironment("Production")` (or `"Staging"`) `WebApplicationFactory` fixture. All three are security-relevant (admin-seed idempotency, GuardCaptcha fail-fast, legacy-creds non-Development gate) but are verified by code review in both security audits. Recommended: implement the Production boot fixture as a follow-up task; confirm with the lead whether this should block the P1-13 merge or be tracked as a follow-up.

**Q4 — Lockout boundary (RESOLVED):**
Confirmed: **lock occurs on attempt 5** with `MaxFailedAccessAttempts=5`. This matches the expected ASP.NET Identity semantics (`IncrementAccessFailedCount` then `>= Max`). No off-by-one bug found. Recorded in BE-TC-04 test and the section above.

**Note on language headers:**
All sign-in tests that assert specific English message strings must pass `Accept-Language: en-US`. The server's `User.PreferredLanguage` defaults to `ar-EG` for all users, so without the header the response carries Arabic strings. This is correct behavior — the tests were updated to include the header explicitly. Arabic-specific tests (BE-TC-08, BE-TC-14) use `Accept-Language: ar-EG` and verify Arabic strings directly.
