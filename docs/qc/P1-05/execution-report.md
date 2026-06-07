# Execution Report — P1-05 Role-based access control (BACKEND)

## Run metadata

| Field | Value |
|---|---|
| Date run (initial) | 2026-06-07 |
| Date run (corrected) | 2026-06-07 |
| Agent | api-tester (initial) / backend-feature (corrected) |
| Branch / commit | main / 8a8124c → feat/grade-authz (corrected) |
| Harness | `LearnexiaWebAppFactory` (env "Testing", Testcontainers PostgreSQL pgvector/pgvector:pg16) |
| Test file(s) | `backend/tests/Learnexia.IntegrationTests/P1_05_RBAC_Tests.cs` |
| Overall verdict | **PASS** (34/34) — security gap (BE-TC-20) fixed; corrected-behavior tests added |

## dotnet test result (verbatim — corrected run)

```
Passed!  - Failed:     0, Passed:    66, Skipped:     0, Total:    66, Duration: 28 s - Learnexia.IntegrationTests.dll (net10.0)
```

(66 total = 34 P1_05 + 32 P2_01 via joint filter `P2_01_CurriculumHierarchy|P1_05_RBAC`.)

Command used:
```
dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P2_01_CurriculumHierarchy|FullyQualifiedName~P1_05_RBAC"
```

## Results

| Case ID | Title | Priority | Result | Actual status / observed | Notes / defect ref |
|---|---|---|---|---|---|
| BE-TC-01 | Role list, no token → 401 | P0 | PASS | HTTP 401 | `AC2_AuthorzationRoleList_NoToken_Returns401` (pre-existing) |
| BE-TC-02 | Role create, no token → 401 | P0 | PASS | HTTP 401 | `AC2_AuthorzationCreate_NoToken_Returns401` (pre-existing) |
| BE-TC-03 | UserManagement AddUser, no token → 401 | P0 | PASS | HTTP 401 | `BeTc03_UserManagementAddUser_NoToken_Returns401` (new) |
| BE-TC-04 | Parent My-Children, no token → 401 | P0 | PASS | HTTP 401 | `BeTc04_ParentMyChildren_NoToken_Returns401` (new) |
| BE-TC-05 | Role list, Parent token → 403 | P0 | PASS | HTTP 403 | `AC1_AC3_AuthorzationRoleList_ParentToken_Returns403` (pre-existing) |
| BE-TC-06 | Role list, Basic token → 403 | P0 | PASS | HTTP 403 | `AC1_AC3_AuthorzationRoleList_BasicToken_Returns403` (pre-existing) |
| BE-TC-07 | Role list, Admin token → 200 + envelope | P0 | PASS | HTTP 200, `successed=true` | `AC3_AuthorzationRoleList_SuperAdminToken_Returns200` (pre-existing) |
| BE-TC-08 | Role create, Parent token → 403 | P1 | PASS | HTTP 403 | `AC1_AC3_AuthorzationCreate_ParentToken_Returns403` (pre-existing) |
| BE-TC-09 | AddUser, non-admin token → 403 | P0 | PASS | HTTP 403 (Parent); HTTP 403 (Basic) | `BeTc09_UserManagementAddUser_ParentToken_Returns403` + `BeTc09b` (new) |
| BE-TC-10 | AddUser, Admin token → not 401/403 | P1 | PASS | HTTP 200 | `BeTc10_UserManagementAddUser_AdminToken_PassesAuthz` (new) |
| BE-TC-11 | Sign-In remains anonymous → not 401 | P0 | PASS | HTTP 200 | `AC8_SignIn_Anonymous_NotUnauthorized` (pre-existing) |
| BE-TC-12 | Register/Validate/Refresh anonymous → not 401 | P1 | PASS | Register=200, Validate=non-401, Refresh=non-401 | `AC8_RegisterParent_Anonymous_NotUnauthorized`, `AC8_ValidateToken_Anonymous_NotUnauthorized`, `AC8_RefreshToken_Anonymous_NotUnauthorized` (pre-existing) |
| BE-TC-13 | Health probes anonymous → 200 | P1 | PASS | HTTP 200 both `/health` and `/health/live` | `AC8_Health_Anonymous_Returns200`, `AC8_HealthLive_Anonymous_Returns200` (pre-existing) |
| BE-TC-14 | Invalid bearer token → 401 (not 500) | P0 | PASS | HTTP 401 | `InvalidToken_AuthorzationRoleList_Returns401_Not500` (pre-existing) |
| BE-TC-15 | Cross-family Link-Child deny (Parent B) | P0 | PASS | non-2xx + `successed=false` | `AC4_FamilyScope_ParentB_DeniedChildLinkedToParentA` (pre-existing) |
| BE-TC-16 | Parent B sees 0 of Parent A's children | P0 | PASS | HTTP 200, `data.length=0` | `AC4_FamilyScope_ParentB_MyChildren_DoesNotSeeParentAChildren` (pre-existing) |
| BE-TC-17 | Actor from JWT, not body (self-scope) | P1 | PASS | Parent B My-Children returns 0 items | `BeTc17_ParentB_ActorFromJwt_SeesOnlyOwnChildren` (new) |
| BE-TC-18 | Admin allowed into ParentController | P2 | PASS | HTTP 200, `successed=true` | `AC4_FamilyScope_AdminToken_Succeeds` (pre-existing) |
| BE-TC-19 | Parent on Student-only quiz route → 403 | P1 | PASS | HTTP 403 | `BeTc19_QuizzesAttempt_ParentToken_Returns403` (new) |
| BE-TC-19b | Policies only for real modules (Learning, Parent) | P2 | PASS | `GenerateModules()` = `["Learning","Parent"]`; no Catalog | `BeTc19b_GenerateModules_IsLearningAndParentOnly` (new) |
| BE-TC-20 | GradesController auth (FIXED) | P0 | PASS | GET List no token → 401; GET List with auth → 200; POST Create no token → 401; POST Create Parent → 403; POST Create Admin → 200/422 (handler reached) | **Security gap closed**. Gap-documenting tests replaced with corrected-behavior tests: `BeTc20_Grades_List_NoToken_Returns401`, `BeTc20b_Grades_List_Authenticated_Returns200`, `BeTc20c_Grades_Create_NoToken_Returns401`, `BeTc20d_Grades_Create_ParentToken_Returns403`, `BeTc20e_Grades_Create_AdminToken_ReachesHandler`. |
| BE-TC-21 | 401 is real HTTP, not fake 200 | P0 | PASS | HTTP status = 401 (not 200 envelope) | `Envelope_401_IsRealHttp401_NotFake200` (pre-existing) |
| BE-TC-22 | 401 on ParentController is real HTTP | P1 | PASS | HTTP 401 on `api/Parent/My-Children` | `BeTc22_Envelope_401_OnParentController_IsRealHttp401` (new) |
| BE-TC-23 | 403 is real HTTP, not fake 200 | P0 | PASS | HTTP status = 403 (not 200 envelope) | `Envelope_403_IsRealHttp403_NotFake200` (pre-existing) |
| BE-TC-24 | appsettings JWT secret is placeholder | P1 | PASS | `JwtSettings.Secret = "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789"` | `BeTc24_AppsettingsJson_JwtSecret_IsPlaceholder` (new) |

**Tally: 24 cases PASS, 0 FAIL, 0 BLOCKED. 34 test methods (some cases covered by multiple methods; BE-TC-20 expanded from 3 gap-documenting methods to 5 corrected-behavior methods).**

---

## Defects found

| # | Case ID | Severity | Summary | Repro | Status |
|---|---|---|---|---|---|
| DEF-01 | BE-TC-20 | HIGH (security) | `GradesController` (`api/learning/Grades/*`) had no `[Authorize]` attribute — all five endpoints were world-accessible without authentication. | See original gap-documenting tests (now replaced by corrected-behavior tests). | **FIXED** — `GradesController` now carries class-level `[Authorize]` (reads) and `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` on Create/Update/Delete. Verified: anonymous GET → 401, authenticated GET → 200, anonymous POST → 401, Parent POST → 403, Admin POST → reaches handler (200/422). |

---

## Coverage sign-off

| Acceptance criterion | Covering cases | Verdict |
|---|---|---|
| Wrong role → 403 (AC-1) | BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-09, BE-TC-23 | PASS |
| Unauthenticated → 401 (AC-2) | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-04, BE-TC-14, BE-TC-21, BE-TC-22 | PASS |
| Students/parents data isolation (AC-4) | BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18 | PASS |
| Parent not a learner (AC-5) | BE-TC-19 | PASS |
| Admin-only curriculum (AC-3) | BE-TC-05 through BE-TC-10 + BE-TC-20 (fixed) | PASS — role/claim CRUD, user mgmt, and `GradesController` writes all gated; `GradesController` reads gated to authenticated users (DEF-01 fixed) |
| Secret out of appsettings (AC-7) | BE-TC-24 | PASS |
| Claims scoped to real modules (AC-6) | BE-TC-19b | PASS |
| Authn/health stay anonymous (AC-8) | BE-TC-11, BE-TC-12, BE-TC-13 | PASS |

---

## Notes for reviewer / lead

### Security Bug — DEF-01 (`GradesController` fully anonymous): FIXED

`GradesController` at `api/learning/Grades/*` previously carried a source comment "AuthZ deliberately omitted for P2-01." This has been resolved by lead decision. The fix applied:

- Class-level `[Authorize]` on `GradesController` — reads (List, GetById) require any authenticated bearer.
- `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` on Create, Update, Delete — require Admin or SuperAdmin role.
- Stale comment removed from the controller.
- `P2_01_CurriculumHierarchy_Tests.cs` updated: grade writes use Admin JWT, grade reads use Admin JWT, AC-6 grade anonymity test flipped to assert 401 (renamed to `AC6_GradesList_RequiresAuth`), parameterized test split into grades-specific + other-five tests.
- `P1_05_RBAC_Tests.cs` BE-TC-20 gap-documenting tests replaced with 5 corrected-behavior tests.

All 66 combined P1_05 + P2_01 tests pass.

### Open questions resolved by this run

- **Q3 (FamilyScopeAuthorizationHandler removed):** Confirmed. The cases assert current per-handler HTTP isolation. BE-TC-15/16/17 all pass, proving the Parent module's link-row check correctly enforces cross-family denial.
- **Q4 (AC-7 secret):** `GuardJwtSecret` in `DependencyInjection.cs` (line 206) throws in Production/Staging if the placeholder is still set; it is tolerant in Development/Testing. BE-TC-24 confirms the committed value is the placeholder. This is the accepted evidence for AC-7.
- **AC-5 "parent is not a learner":** Confirmed via BE-TC-19 — `POST api/Learning/Quizzes/999/Attempt` with a Parent token returns HTTP 403.
