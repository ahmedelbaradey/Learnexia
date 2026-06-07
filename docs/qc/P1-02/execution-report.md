# Execution Report — P1-02 (Stay signed in: refresh & sign-out)

> **Filled by `api-tester` after running the suite.** QC (test architect) leaves this empty — it only scaffolds the template.
> **Test file:** `backend/tests/Learnexia.IntegrationTests/P1_02_RefreshAndSignOut_Tests.cs`
> **Run command:** `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P1_02"`
> Status values: PASS / FAIL / BLOCKED / SKIPPED. For FAIL or BLOCKED, fill the Defect / blocker column.

## Run metadata

| Field | Value |
|---|---|
| Date run | 2026-06-07 |
| Commit / branch | main (8a8124c) |
| `dotnet test` exit code | 0 |
| Total / Pass / Fail / Blocked | 26 / 26 / 0 / 1 (BE-TC-11, excluded from run count — no test method) |
| Redis backing in run | In-memory `IDistributedCache` fallback (appsettings `"Redis": ""`) |
| Expired-JWT seam reached 200 (BE-TC-05)? | Yes — `AC-1 HappyPath_Refresh_Returns200_NewAccessToken` and all seam-dependent cases reached 200. Test signing key `CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789` confirmed matching. |

## Results

| Case ID | Title | Priority | Mapped `[Fact]` | Status | Defect / blocker / notes |
|---|---|---|---|---|---|
| BE-TC-01 | Sign-In returns non-empty refresh token | P0 | `AC2a_SignIn_ReturnsRefreshToken` | PASS | |
| BE-TC-02 | Register-Parent returns non-empty refresh token | P0 | `AC2b_Register_ReturnsRefreshToken` | PASS | |
| BE-TC-03 | Refresh token persisted/retrievable (Redis round-trip) | P0 | `BeTc03_RefreshToken_PersistenceRoundTrip` | PASS | Added in this run (was gap). |
| BE-TC-04 | Refresh token carries 7-day expireAt | P1 | `AC6_RefreshTokenExpiry_Is7Days` | PASS | Also covers BE-TC-20. |
| BE-TC-05 | Valid expired access + matching refresh → 200 new access token *(seam canary)* | P0 | `AC1_HappyPath_Refresh_Returns200_NewAccessToken` | PASS | Seam confirmed: 200 reached. Signing key matches `appsettings.json`. |
| BE-TC-06 | New access token is a readable valid JWT | P1 | `BeTc06_NewAccessToken_IsReadableJwt` | PASS | Added in this run (was gap). Carries seam guard inline. |
| BE-TC-07 | Refresh preserves identity claims | P1 | `BeTc07_Refresh_PreservesIdentityClaims` | PASS | Added in this run (was gap). Checks `Id` and `NameIdentifier` claims. |
| BE-TC-08 | Tampered refresh token → 401 | P0 | `AC5_TamperedRefreshToken_Returns401` | PASS | |
| BE-TC-09 | Garbage refresh token → 401 (not 500) | P0 | `AC4_GarbageRefreshToken_Returns401_NotServerError` | PASS | |
| BE-TC-10 | No stored token → 401 (RefreshTokenNotFound) | P1 | `BeTc10_NoStoredToken_Returns401` | PASS | Added in this run (was gap). Sign-out first, then attempt refresh. |
| BE-TC-11 | Expired stored refresh token → 401 (RefreshTokenIsExpired) | P1 | *(no test method)* | BLOCKED | No time/TTL seam. The 7-day wall-clock window cannot be waited in-suite, and no clock seam exists for the cache TTL. Covered indirectly by BE-TC-15 (revoked path) and `RefreshTokenValidatiorTests` unit test. Lead decision required to add a seam (README Open Q2). |
| BE-TC-12 | /Refresh-Token never returns 500 for auth failures | P0 | `AC4_GarbageRefreshToken_Returns401_NotServerError` + `AC4_Envelope_401_HasCorrectShape` | PASS | Both tests assert `!= 500` and correct 401 envelope. |
| BE-TC-13 | Sign-Out with Bearer → 200 Successed=true | P0 | `AC3a_SignOut_Returns200_Successed` | PASS | |
| BE-TC-14 | Sign-Out persists revocation (entry removed) | P0 | `AC3b_SignOut_InvalidatesRefreshToken` | PASS | |
| BE-TC-15 | Revoked (signed-out) refresh → 401 | P0 | `BeTc15_RevokedRefreshToken_Returns401` | PASS | Added in this run (was gap). Explicit behavioural contract test. |
| BE-TC-16 | Rotation: refreshed token differs from supplied | P0 | `AC8_Rotation_NewRefreshTokenDiffersFromOld` | PASS | |
| BE-TC-17 | Rotation: new refresh token carries 7-day expireAt | P2 | `BeTc17_Rotation_NewRefreshToken_Carries7DayExpiry` | PASS | Added in this run (was gap). ±60 s tolerance. |
| BE-TC-18 | TokenIsRunning: fresh access token → 400 (not 401/500) | P0 | `AC1_TokenIsRunning_NeverReturns500` | PASS | |
| BE-TC-19 | Access token lifetime ≈ 30 min | P1 | `AC6_AccessTokenLifetime_Is30Min` | PASS | ±2 min tolerance. |
| BE-TC-20 | Refresh token lifetime ≈ 7 days (sign-in) | P1 | `AC6_RefreshTokenExpiry_Is7Days` | PASS | Same test as BE-TC-04. |
| BE-TC-21 | Replay: original refresh after rotation → 401 | P0 | `AC8_Replay_OldRefreshToken_After_Rotation_Returns401` | PASS | |
| BE-TC-22 | Missing accessToken → 422 with Errors[] | P0 | `AC7a_MissingAccessToken_Returns422` | PASS | |
| BE-TC-23 | Missing refreshToken → 422 with Errors[] | P0 | `AC7b_MissingRefreshToken_Returns422` | PASS | |
| BE-TC-24 | Both fields missing → 422, error per field | P1 | `AC7c_BothMissing_Returns422` | PASS | Extended in this run to assert both `AccessToken` and `RefreshToken` appear in errors array. |
| BE-TC-25 | Sign-Out without Bearer → 401 | P0 | `Auth_SignOut_WithoutBearer_Returns401` | PASS | |
| BE-TC-26 | Sign-Out with malformed Bearer → 401 | P1 | `BeTc26_SignOut_MalformedBearer_Returns401` | PASS | Added in this run (was gap). |
| BE-TC-27 | Sign-Out idempotent (second call no 500) | P2 | `BeTc27_SignOut_Idempotent_SecondCallNoServerError` | PASS | Added in this run (was gap). Second sign-out accepted 200 or 401 — never 500. |
| BE-TC-28 | Regression: P1-01 sign-in still works | P1 | `Regression_P1_01_SuperAdmin_CanStillSignIn` | PASS | |

## Defects found

| # | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| — | — | — | No defects found. All 26 testable cases pass. | — |

## Coverage verdict

- **All AC-1..AC-8 exercised by at least one passing case?** Yes.
  - AC-1: BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-18 — all PASS
  - AC-2: BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-04 — all PASS
  - AC-3: BE-TC-13, BE-TC-14, BE-TC-15 — all PASS
  - AC-4: BE-TC-09, BE-TC-10, BE-TC-12, BE-TC-15 — all PASS (BE-TC-11 BLOCKED, covered by BE-TC-15 + unit test)
  - AC-5: BE-TC-08, BE-TC-09, BE-TC-21 — all PASS
  - AC-6: BE-TC-18, BE-TC-19, BE-TC-20 — all PASS
  - AC-7: BE-TC-22, BE-TC-23, BE-TC-24 — all PASS
  - AC-8: BE-TC-16, BE-TC-17, BE-TC-21 — all PASS
- **Any P0 case failing or blocked?** No. All 17 P0 cases PASS. BE-TC-11 (P1, BLOCKED) is the only blocked case.
- **Seam reliability (BE-TC-05 reached 200)?** Yes. `AC-1 HappyPath_Refresh_Returns200_NewAccessToken` confirms the expired-JWT forge reaches the 200 happy path. The signing key in `appsettings.json` (`CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789`) matches the test forge key. All seam-dependent cases (BE-TC-06, 07, 08, 09, 10, 15, 16, 17, 21) are reliable.
- **Regression suite (P1-01) green?** Yes — `Regression_P1_01_SuperAdmin_CanStillSignIn` PASS.

## Open items carried forward

1. **BE-TC-11 BLOCKED (no time seam).** The `RefreshTokenIsExpired` 401 path (stored `ExpiryDate < UtcNow`) is not directly testable via the public API without a clock/TTL injection seam for `IDistributedCache`. Mitigation: the code path is covered by the existing `RefreshTokenValidatiorTests` unit test and by the analogous not-found branch (BE-TC-10/15). Lead decision: add a seam or accept as unit-test only (README Open Q2).
2. **`RegistrationIsCompleted` column** (warning in EF migration output — not a P1-02 issue). Noted for traceability; does not affect these tests.

---
*`reviewer` consumes this report at the batch gate. Do not edit the case catalog (`backend-test-cases.md`) — record divergences here instead.*
