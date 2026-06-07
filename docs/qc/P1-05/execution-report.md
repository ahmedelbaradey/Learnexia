# Execution Report — P1-05 Role-based access control (BACKEND)

## Run metadata

| Field | Value |
|---|---|
| Date run | 2026-06-07 |
| Agent | api-tester |
| Branch / commit | main / 8a8124c |
| Harness | `LearnexiaWebAppFactory` (env "Testing", Testcontainers PostgreSQL pgvector/pgvector:pg16) |
| Test file(s) | `backend/tests/Learnexia.IntegrationTests/P1_05_RBAC_Tests.cs` |
| Overall verdict | **PASS** (32/32) with 1 documented security gap (BE-TC-20) |

## dotnet test result (verbatim)

```
Passed!  - Failed:     0, Passed:    32, Skipped:     0, Total:    32, Duration: 15 s - Learnexia.IntegrationTests.dll (net10.0)
```

Command used:
```
dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P1_05"
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
| BE-TC-20 | GradesController anonymous (GAP) | P0 (finding) | PASS (gap confirmed) | GET List = HTTP 200 no token; POST Create = HTTP 200/400 no token; DELETE = non-401 no token | **SECURITY BUG — see defects below**. Three sub-tests document all three verbs. |
| BE-TC-21 | 401 is real HTTP, not fake 200 | P0 | PASS | HTTP status = 401 (not 200 envelope) | `Envelope_401_IsRealHttp401_NotFake200` (pre-existing) |
| BE-TC-22 | 401 on ParentController is real HTTP | P1 | PASS | HTTP 401 on `api/Parent/My-Children` | `BeTc22_Envelope_401_OnParentController_IsRealHttp401` (new) |
| BE-TC-23 | 403 is real HTTP, not fake 200 | P0 | PASS | HTTP status = 403 (not 200 envelope) | `Envelope_403_IsRealHttp403_NotFake200` (pre-existing) |
| BE-TC-24 | appsettings JWT secret is placeholder | P1 | PASS | `JwtSettings.Secret = "CHANGE_ME_super_secret_key_at_least_32_chars_long_0123456789"` | `BeTc24_AppsettingsJson_JwtSecret_IsPlaceholder` (new) |

**Tally: 24 cases PASS, 0 FAIL, 0 BLOCKED. 32 test methods (some cases covered by multiple methods).**

---

## Defects found

| # | Case ID | Severity | Summary | Repro | Status |
|---|---|---|---|---|---|
| DEF-01 | BE-TC-20 | HIGH (security) | `GradesController` (`api/learning/Grades/*`) has no `[Authorize]` attribute — all five endpoints (List, GetById, Create, Update, Delete) are world-accessible without authentication. Unauthenticated users can read all curriculum grades AND create/update/delete them. This directly contradicts the story AC "admin-only curriculum endpoints reject non-admins." | `GET api/learning/Grades/List` with no token → HTTP 200; `POST api/learning/Grades/Create` with no `Authorization` header + body `{ Number: 99, DisplayName: "test" }` → HTTP 200/400 (handler runs); `DELETE api/learning/Grades?id=99999` with no token → handler runs (not 401). Tests `BeTc20_Grades_List_NoToken_IsAnonymous_SecurityGap`, `BeTc20b_Grades_Create_NoToken_IsAnonymous_SecurityGap`, `BeTc20c_Grades_Delete_NoToken_IsAnonymous_SecurityGap` all pass by documenting this insecure state. | OPEN — filed back to `backend-feature`. **Fix:** add `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` at class level on `GradesController`. Reads may optionally be `[Authorize]` only (not AdminOnly). |

---

## Coverage sign-off

| Acceptance criterion | Covering cases | Verdict |
|---|---|---|
| Wrong role → 403 (AC-1) | BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-09, BE-TC-23 | PASS |
| Unauthenticated → 401 (AC-2) | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-04, BE-TC-14, BE-TC-21, BE-TC-22 | PASS |
| Students/parents data isolation (AC-4) | BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18 | PASS |
| Parent not a learner (AC-5) | BE-TC-19 | PASS |
| Admin-only curriculum (AC-3) | BE-TC-05 through BE-TC-10 + BE-TC-20 (gap) | PARTIAL — role/claim CRUD and user mgmt gated; `GradesController` is NOT gated (DEF-01) |
| Secret out of appsettings (AC-7) | BE-TC-24 | PASS |
| Claims scoped to real modules (AC-6) | BE-TC-19b | PASS |
| Authn/health stay anonymous (AC-8) | BE-TC-11, BE-TC-12, BE-TC-13 | PASS |

---

## Notes for reviewer / lead

### Security Bug — DEF-01 (`GradesController` fully anonymous): action required from `backend-feature`

`GradesController` at `api/learning/Grades/*` carries a source comment "AuthZ deliberately omitted for P2-01" — the authz was intentionally deferred. However, the story AC for P1-05 explicitly requires "admin-only curriculum endpoints reject non-admins." The current state is:

- `GET api/learning/Grades/List` — no token required → returns all grades
- `POST api/learning/Grades/Create` — no token required → creates a grade
- `PUT api/learning/Grades/Update` — no token required → edits a grade
- `DELETE api/learning/Grades?id=...` — no token required → deletes a grade

**Requested fix for `backend-feature`:** Add `[Authorize(Policy = AuthorizationPolicies.AdminOnly)]` at the `GradesController` class level. This is a one-line change. Once applied, re-run the BE-TC-20 tests — they will fail (because they assert the *current insecure state*), which means the gap is fixed. The tests should be updated by `backend-feature` or `api-tester` to assert the **desired** 401/403 behavior after the fix.

### Open questions resolved by this run

- **Q3 (FamilyScopeAuthorizationHandler removed):** Confirmed. The cases assert current per-handler HTTP isolation. BE-TC-15/16/17 all pass, proving the Parent module's link-row check correctly enforces cross-family denial.
- **Q4 (AC-7 secret):** `GuardJwtSecret` in `DependencyInjection.cs` (line 206) throws in Production/Staging if the placeholder is still set; it is tolerant in Development/Testing. BE-TC-24 confirms the committed value is the placeholder. This is the accepted evidence for AC-7.
- **AC-5 "parent is not a learner":** Confirmed via BE-TC-19 — `POST api/Learning/Quizzes/999/Attempt` with a Parent token returns HTTP 403.
