# Execution Report — P1-03 (Backend Add-Child)

> Implemented from: `backend-test-cases.md`. Owner of results: **`api-tester`**.

## Run metadata
| Field | Value |
|---|---|
| Branch / commit | main / 2f14700 |
| Date run | 2026-06-07 |
| Environment (API base URL, DB) | In-process WebApplicationFactory + Testcontainers pgvector/pg16 |
| Backend build status | Build succeeded (0 errors, 9 warnings) |
| Tester (agent) | api-tester |

## Summary
| Metric | Count |
|---|---|
| Total cases | 30 (28 core + BE-TC-12b + BE-TC-30) |
| Passed | 69 test methods covering 29 cases |
| Failed | 1 (BE-TC-30) |
| Blocked | 1 (BE-TC-27) |
| Not run | 0 |
| Defects filed | 1 (DEF-P103-01) |

## Per-case results

| ID | Title | Priority | Expected HTTP | HTTP got | Result | Notes / defect ref |
|---|---|---|---|---|---|---|
| BE-TC-01 | Parent adds valid child → 200 + Student account | P0 | 200 | 200 | PASS | AC1a/AC1b/AC1c/AC1d + AC1d (Student role DB check) |
| BE-TC-02 | Login email = parent-assigned value | P0 | 200 | 200 | PASS | `Data.email` echoed byte-for-byte in AC1a |
| BE-TC-03 | Grade/language/country/learningLanguage persisted | P0 | 200 | 200 | PASS | `BETC03_AllProfileFields_PersistedAndListedInMyChildren` confirms all 4 fields in My-Children |
| BE-TC-04 | Two children in one session → both listed | P0 | 200 | 200 | PASS | `AC2_MultipleChildren_BothAppearInMyChildren` + `AC2_MultipleChildren_EachHasDistinctId` |
| BE-TC-05 | Duplicate after sibling does not undo the sibling | P1 | 400 (step 2) | 400 | PASS | `BETC05_DuplicateAfterSibling_DoesNotUndoSibling` |
| BE-TC-06 | Grade 0 → 422 | P0 | 422 | 422 | PASS | `AC6_Grade0_Returns422` |
| BE-TC-07 | Grade 7 → 422 | P0 | 422 | 422 | PASS | `AC6_Grade7_Returns422` |
| BE-TC-08 | Grade -1 / 1000 → 422 | P1 | 422 | 422 | PASS | `AC6_GradeNegative_Returns422` + `BETC08b_Grade1000_Returns422` |
| BE-TC-09 | Empty password → 422 | P0 | 422 | 422 | PASS | `AC6_EmptyPassword_Returns422_WithErrors` |
| BE-TC-10 | Password fails complexity → 422 | P0 | 422 | 422 | PASS | `BETC10_PasswordFailsComplexity_Returns422` (all 5 sub-runs via Theory: no-upper, no-lower, no-digit, no-special, too-short) |
| BE-TC-11 | Minimum-valid password → 200 | P1 | 200 | 200 | PASS | `BETC11_MinimumValidPassword_Returns200` (`Aa1!aa`, len=6, all 4 classes) |
| BE-TC-12 | `language` not in {ar,en} → 422 | P0 | 422 | 422 | PASS | `AC6_InvalidLanguage_Fr_Returns422` + `BETC12b_Language_UppercaseEN_Returns422` + `BETC12c_Language_Empty_Returns422` + `AC6_InvalidLanguage_FullCode_Returns422` |
| BE-TC-12b | `country` whitespace-only | P2 | 422 | 422 | PASS | `BETC12b_CountryWhitespaceOnly_Returns422` — NotEmpty() treats whitespace-only as empty |
| BE-TC-13 | `learningLanguage` missing/invalid → 422 | P0 | 422 | 422 | PASS | `P8_01_MissingLearningLanguage_Returns422` + `P8_01_InvalidLearningLanguage_Returns422` + `BETC13c_LearningLanguage_Empty_Returns422` |
| BE-TC-14 | Malformed email → 422 | P0 | 422 | 422 | PASS | `AC6_MalformedEmail_Returns422_WithErrors` + `AC6_EmptyEmail_Returns422_WithErrors` + `BETC14_MalformedEmail_SubRuns_Returns422` (foo@, @bar.com via Theory) |
| BE-TC-15 | No JWT → 401 | P0 | 401 | 401 | PASS | `AC4_Anonymous_AddChild_Returns401` |
| BE-TC-16 | Expired/malformed JWT → 401 | P1 | 401 | 401 | PASS | `BETC16_GarbageBearerToken_Returns401` (garbage bearer token sub-run) |
| BE-TC-17 | Student-role token → 403 | P0 | 403 | 403 | PASS | `AC4_StudentRole_AddChild_Returns403` |
| BE-TC-18 | Body cannot inject role/parentId | P0 | 200 (Student only) | 200 | PASS | `BETC18_ExtraRoleFields_AreIgnored_ChildIsStudent` — DB-verified Student role only, Admin/SuperAdmin absent |
| BE-TC-19 | Auto-link to acting parent | P0 | 200 | 200 | PASS | `AC5_AutoLink_ChildAppearsInMyChildren` |
| BE-TC-20 | Child not visible under other parent (family scope) | P0 | n/a (absent) | absent | PASS | `BETC20_CrossFamilyScope_ChildNotVisibleUnderOtherParent` |
| BE-TC-21 | Grade boundaries 1 and 6 persist | P1 | 200 | 200 | PASS | `BETC21_GradeBoundaries_PersistedCorrectly` (Theory grade=1 and grade=6, My-Children grade check) |
| BE-TC-22 | Child signs in with assigned email+password | P0 | 200 | 200 | PASS | `AC3_ChildSignIn_WithAssignedCredentials_Returns200_WithToken` + `BETC22_ChildSignIn_JWT_CarriesLearningLanguageClaim` (JWT claim decoded and verified) |
| BE-TC-23 | Duplicate email → 400 specific message, no account | P0 | 400 | 400 | PASS | `AC7_DuplicateEmail_Returns400_WithSuccessedFalse` + `AC7_DuplicateEmail_ResponseHasSpecificMessage_NotRawIdentityError` + `AC7_DuplicateEmail_NoSecondAccountCreated` |
| BE-TC-24 | Duplicate response same regardless of owner | P1 | 400 (×3, identical) | 400 | PASS | `BETC24_DuplicateEmailResponse_SameRegardlessOfOwner` — all 3 sub-runs return same message |
| BE-TC-25 | Blank fullName → 422 | P1 | 422 | 422 | PASS | `AC6_EmptyFullName_Returns422` + `BETC25b_WhitespaceOnlyFullName_Returns422` |
| BE-TC-26 | Blank country → 422 | P1 | 422 | 422 | PASS | `AC6_EmptyCountry_Returns422` |
| BE-TC-27 | Role-assign-fail compensating delete | P2 | 500 + no orphan | — | BLOCKED | No fault-injection hook to force `AddToRoleAsync` failure from the HTTP layer. See Blocked section. |
| BE-TC-29 | Admin/SuperAdmin token can call (support) | P2 | 200 | 200 | PASS | `AC4_SuperAdmin_AddChild_Returns200` + `BETC29_SuperAdmin_AddChild_ChildLinkedToSuperAdmin` |
| BE-TC-30 | Oversized inputs → no 500 | P2 | 422/400/200 | **500** | **FAIL** | `BETC30_OversizedFullName_NoUnhandled500` — 10k-char fullName hits a Postgres column-length violation, caught by the handler's generic catch, returned as 500. See DEF-P103-01. |

## Defects found

| Defect ID | Case(s) | Severity | Summary | Filed to | Status |
|---|---|---|---|---|---|
| DEF-P103-01 | BE-TC-30 | Medium | An oversized `fullName` (~10,000 chars) that exceeds the Postgres column length causes a DB exception, which the handler's generic `catch` converts to a 500 (`SystemErrorSavingData`). The validator has no `MaximumLength` rule on `FullName`, so the input passes validation (200 envelope path) and only fails at the DB `INSERT`. A `MaximumLength(N)` rule in `AddChildCommandValidator` would catch it at 422 and prevent the 500. Expected: 422 (or at most a graceful 400). Actual: 500 with `{"statusCode":500,"successed":false,"message":"حدث خطأ أثناء حفظ البيانات","data":null,"errors":[]}`. | backend-feature | Open |

## Blocked cases — reason

| ID | Blocker |
|---|---|
| BE-TC-27 | No fault-injection hook exists to force `AddToRoleAsync` failure from the HTTP surface. The compensating-delete logic is in `IChildAccountService.CreateChildAsync` inside the Identity module. To test it a test seam (e.g. a test-double `IChildAccountService` that throws after `CreateAsync` but before `AddToRoleAsync`) would need to be injectable via the test host. Without that seam the 500 + no-orphan postcondition cannot be triggered over HTTP. |

## Coverage map — acceptance criteria

| AC | Criterion | Tests | Result |
|---|---|---|---|
| AC-1 | Parent adds child with grade/language/country → child created w/ Student role | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-11, BE-TC-21 | COVERED |
| AC-2 | Parent adds more than one child; each gets a distinct account | BE-TC-04, BE-TC-05 | COVERED |
| AC-3 | Child login email = parent-assigned value; child can authenticate with it | BE-TC-02, BE-TC-22 | COVERED |
| AC-4 | Parent-only endpoint; acting parent from JWT; no child self-create path | BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18, BE-TC-28 | COVERED |
| AC-5 | Created child auto-linked to acting parent; appears in My-Children | BE-TC-19, BE-TC-20, BE-TC-04 | COVERED |
| AC-6 | Validation → 422: grade outside 1–6, blank required fields, malformed email, language not in {ar,en} | BE-TC-06..BE-TC-14, BE-TC-25, BE-TC-26 | COVERED |
| AC-7 | Duplicate email → 400 specific message, no account created; siblings unaffected | BE-TC-23, BE-TC-24, BE-TC-05 | COVERED |
| AC-8 | Child stored language drives locale (persistence only; consumption is P1-09) | BE-TC-03, BE-TC-21, BE-TC-22 | COVERED |

## Tester sign-off
- **Overall verdict: FAIL**
- Unexpected 500s observed: **1** — BE-TC-30 (oversized fullName, no max-length validator in `AddChildCommandValidator`). DEF-P103-01 filed to `backend-feature`.
- Notes for reviewer: All P0 and P1 cases pass. The single failure (BE-TC-30, P2) is a missing `MaximumLength` rule in the validator. BE-TC-27 (P2) is BLOCKED — no HTTP fault-injection seam exists.
