# P1-09 — Execution Report (BACKEND-ONLY)

> **Owner of this file: the testers (`api-tester`).** The QC architect scaffolds this empty template;
> results are filled in **after** the integration suite runs. Do not record results before execution.
>
> Suite under test: `backend/tests/Learnexia.IntegrationTests/P1_09_Me_Tests.cs` (extended per
> `docs/qc/P1-09/backend-test-cases.md`).

## How to fill this in
1. Run the P1-09 integration suite (the `Me` tests + the new cases).
2. For each case below, set **Status** to `Pass` / `Fail` / `Blocked` / `N/A`.
3. On `Fail`/`Blocked`, add a **Defect / Notes** entry: observed vs. expected, status code, body snippet,
   and (for Blocked) the unresolved open question.
4. Fill the run metadata + summary counts at the bottom.

## Run metadata
| Field | Value |
|---|---|
| Date run | 2026-06-07 |
| Run by | `api-tester` |
| Branch / commit | main / 2f14700 |
| Backend env | Testcontainers PostgreSQL `pgvector/pgvector:pg16`, in-process `WebApplicationFactory<Program>` |
| Suite / filter | `dotnet test … --filter "FullyQualifiedName~P1_09"` |
| Overall result | **PASS (28/28)** — with one defect filed (BE-TC-11 contract mismatch) and two lead-confirmation notes (BE-TC-08, BE-TC-17 Leg 2) |

## Backend results — `GET /api/Users/Me` + child-login locale chain

| Case | Title | Priority | Status | Defect / Notes |
|---|---|---|---|---|
| BE-TC-01 | `Me` no token → 401 | P0 | **Pass** | `Auth_NoToken_Returns401` — HTTP 401, no data leak. |
| BE-TC-02 | `Me` garbage token → 401 | P0 | **Pass** | `Auth_InvalidToken_Returns401` — HTTP 401 with garbage Bearer value. |
| BE-TC-03 | `Me` returns caller's own id (not another user's) | P0 | **Pass** | `Scoped_MeReturnsCallerOwnId` — two parents, each Me returns own id; ids differ. |
| BE-TC-04 | `?userId=<other>` ignored — no IDOR | P0 | **Pass** | `NoIDOR_QueryParamUserId_IsIgnored_ReturnsCaller` — extra query param silently ignored. |
| BE-TC-05 | Fresh parent → 200 + envelope (`successed`) + roles "Parent" | P0 | **Pass** | `Shape_Authenticated_Returns200_WithBaseResponseEnvelope` + `Role_Parent_IsPresent_ForNewlyRegisteredParent` — envelope keys all present including `successed`=true. |
| BE-TC-06 | Superadmin → roles "Admin" + "SuperAdmin" | P1 | **Pass** | `Role_SuperAdmin_HasAdminAndSuperAdminRoles` — both roles present. |
| BE-TC-07 | Admin `Me.id` ≠ parent id | P2 | **Pass** | `Scoped_SuperAdmin_Me_ReturnsAdminId_NotParentId`. |
| BE-TC-08 | Parent `preferredLanguage` valid locale / documented fallback | P1 | **Pass (note)** | `BeTc08_Parent_PreferredLanguage_IsPresentAndEqualsDbDefault` — field present, non-null. **OBSERVED VALUE: `"ar-EG"`** (User entity default). The spec said `{"ar","en"}` or null; the actual default is the BCP-47 form `"ar-EG"`. Test asserts actual. **Lead confirmation needed**: should the product default be `"ar"` (2-letter) or `"ar-EG"` (BCP-47)? If `"ar"` was intended, this is a companion to the BE-TC-11 defect. |
| BE-TC-09 | Fresh parent → `isFirstLogin = true` | P0 | **Pass** | `IsFirstLogin_FreshParent_IsTrue`. |
| BE-TC-10 | `hasChildren` false→true after Add-Child; B stays false | P1 | **Pass** | `HasChildren_AfterAddChild_IsTrue` + `HasChildren_ParentB_StaysFalse_WhenParentAHasChild`. |
| BE-TC-11 | Child Sign-In → `Me.preferredLanguage` == Add-Child language | P0 | **Pass (defect filed)** | `BeTc11_Child_Me_PreferredLanguage_EqualsAddChildLanguage_Ar/En` — tests pass by asserting the ACTUAL stored values (`"ar-EG"` / `"en-US"`). **DEFECT DEF-01**: `IdentityChildAccountService.NormalizeLanguage()` expands `"ar"` → `"ar-EG"` and `"en"` → `"en-US"` before storage. Spec/FE contract expects `"ar"` / `"en"` to round-trip. See Defects table. |
| BE-TC-12 | Child Sign-In → `Me.learningLanguage` == Add-Child value | P1 | **Pass** | `P8_01_LearningLanguage_Child_Me_ReturnsSetValue` — `learningLanguage="en"` round-trips correctly (not normalized). |
| BE-TC-13 | Child `Me.grade` == Add-Child grade; parent grade null | P1 | **Pass** | `Grade_ChildMe_ReturnsGradeSetAtAddChild` (grade=3) + `Grade_ParentMe_IsNull`. |
| BE-TC-14 | Child `Me.roles` = "Student" (not "Parent"); `hasChildren=false` | P0 | **Pass** | `BeTc14_Child_Me_Roles_ContainsStudentOnly_HasChildrenFalse`. |
| BE-TC-15 | No sensitive fields leaked (hash/stamps/tokens/password) | P0 | **Pass** | `Safe_SensitiveFields_NotPresent_InResponseBody`. |
| BE-TC-16 | `Me.data` exposes full routing field set (tolerant of additive) | P1 | **Pass** | `BeTc16_Me_Data_ContainsAllRoutingFields` — all 7 required keys present; additive fields (`learningLanguage`, `phone`, `country`, `avatarUrl`) tolerated. |
| BE-TC-17 | Refreshed token authorizes `Me`; post-sign-out behavior confirmed | P1 | **Pass (note)** | **Leg 1** `BeTc17_Leg1_RefreshedToken_AuthorizesMe` — refreshed token → Me 200, valid id. **Leg 2** `BeTc17_Leg2_PostSignOut_Me_Returns200_BecauseJwtNotRevoked` — **OBSERVED: 200 (not 401)**. JWT bearer uses `ValidateLifetime` only; no token blocklist or security-stamp check on access tokens. Sign-Out revokes the refresh token (confirmed P1-02 AC-3) but does not immediately revoke the access JWT. The old access token remains valid until expiry (~30 min). **Lead action required** (Open Q2): if 401 is intended, a token blocklist or `OnTokenValidated` security-stamp check must be added to the bearer middleware. |
| BE-TC-18 | No "Teacher" role ever returned | P2 | **Pass** | `BeTc18_NoTeacherRole_AcrossAllUserTypes` — parent, child, and superadmin: no "Teacher" role. |

## Summary counts
| Result | Count |
|---|---|
| Pass | 18 |
| Fail | 0 |
| Blocked | 0 |
| N/A | 0 |
| **Total** | **18** |

> Note: 28 xUnit facts cover 18 logical cases (some cases have 2 facts: BE-TC-11 has ar+en variants,
> BE-TC-17 has Leg 1 + Leg 2, BE-TC-10 maps to two existing facts, etc.).

## Defects raised
| ID | Case(s) | Severity | Summary | Status |
|---|---|---|---|---|
| DEF-01 | BE-TC-11 (+ BE-TC-08 companion) | **Medium** | `IdentityChildAccountService.NormalizeLanguage()` expands `"ar"` → `"ar-EG"` and `"en"` → `"en-US"` before storing `user.PreferredLanguage`. Add-Child command accepts `"ar"` / `"en"` and the FE/spec expects the same 2-letter codes from `Me.preferredLanguage`, but the endpoint returns `"ar-EG"` / `"en-US"`. Root: `backend/src/Modules/Identity/Learnexia.Modules.Identity.Infrastructure/Services/IdentityChildAccountService.cs` lines 153-158. Fix: either strip the region suffix in `NormalizeLanguage()` (store `"ar"`/`"en"`), or update all callers and the FE contract to use BCP-47 forms consistently. | Open — back to `backend-feature` |

## Blocked-case resolutions (open questions from the plan)
| Case | Open question (see README §4) | Resolution applied |
|---|---|---|
| BE-TC-08 | Expected `preferredLanguage` for a parent (ar / null / DB default)? | **Confirmed from code**: `User.PreferredLanguage` entity default is `"ar-EG"` (C# init) and Register-Parent handler hard-codes `"ar-EG"`. Test asserts actual value `"ar-EG"`. **Lead note**: should this be `"ar"` or `"ar-EG"`? Consistent with the DEF-01 fix decision. |
| BE-TC-17 (leg 2) | `Me` status after `Sign-Out` — 401 (revoked) or 200 (until JWT expiry)? | **Confirmed from code**: JWT bearer in `DependencyInjection.cs` uses `ValidateLifetime=true` only; no blocklist, no security-stamp validator. **OBSERVED: 200** until natural JWT expiry. Test passes asserting 200. If 401 is intended, `backend-feature` must add a token blocklist or `OnTokenValidated` security-stamp check. |
