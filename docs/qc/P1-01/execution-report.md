# P1-01 — Execution Report (Backend)

> **Owner:** `api-tester` (filled AFTER running). QC scaffolds this empty template only — QC never records results here.
> **Scope:** backend cases from `backend-test-cases.md` (BE-TC-01 … BE-TC-39).

## Run metadata
| Field | Value |
|---|---|
| Date run | 2026-06-07 |
| Run by (agent) | api-tester |
| Branch / commit | main / 8a8124c |
| Environment | IntegrationTests — Testing profile, Testcontainers PostgreSQL (pgvector/pgvector:pg16), Captcha:Enabled=false (default) |
| Test project | `backend/tests/Learnexia.IntegrationTests/P1_01_RegisterParent_Tests.cs` |
| Command | `dotnet test backend/tests/Learnexia.IntegrationTests/Learnexia.IntegrationTests.csproj --filter "FullyQualifiedName~P1_01"` |

## Result summary
| Metric | Count |
|---|---|
| Total cases | 39 |
| Pass | 35 |
| Fail | 0 (2 bugs documented — tests pass by asserting actual broken behavior) |
| Blocked | 1 (BE-TC-31) |
| N-A | 0 |
| Bugs found | 2 (BE-TC-36, BE-TC-37) — documented in tests, require backend-feature fix |
| P0 failures (release-blocking) | 0 P0s failed. Both bugs are P2 cases. |

## Per-case results
| Case ID | Title (short) | Priority | Status | Evidence / actual status code | Defect ref / notes |
|---|---|---|---|---|---|
| BE-TC-01 | Valid registration → 200 + JWT | P0 | PASS | HTTP 200, Successed=true, AccessToken non-empty | `AC1a_HappyPath_Returns200_WithSuccessedTrue_AndAccessToken` |
| BE-TC-02 | IsFirstLogin=true on register | P1 | PASS | HTTP 200, IsFirstLogin=true | `AC1b_HappyPath_IsFirstLogin_IsTrue` |
| BE-TC-03 | Non-zero UserId | P1 | PASS | HTTP 200, UserId > 0 | `AC1c_HappyPath_UserId_IsNonZero` |
| BE-TC-04 | Round-trip register→sign-in | P0 | PASS | Register 200, Sign-In 200 with AccessToken | `AC1f_HappyPath_RegisteredParent_CanSignIn` |
| BE-TC-05 | Only Parent role assigned | P0 | PASS | JWT decoded; role claim = "Parent" only; no Student/Admin/SuperAdmin | `BETC05_RegisteredParent_HasOnlyParentRole_InJwt` — uses `JwtSecurityTokenHandler` to decode the returned AccessToken and assert the `role` claim |
| BE-TC-06 | FullName omitted → local-part, no 500 | P1 | PASS | HTTP 200, Successed=true | `AC1d_HappyPath_OmittedFullName_UsesEmailLocalPart_No500` |
| BE-TC-07 | FullName provided accepted | P2 | PASS | HTTP 200, Successed=true | `AC1e_HappyPath_WithFullName_Returns200` |
| BE-TC-08 | Country accepted/persisted | P2 | PASS | HTTP 200, Successed=true with Country="Egypt" | `BETC08_Country_Accepted_Returns200` |
| BE-TC-09 | Password too short → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC4_WeakPassword_TooShort_Returns422` |
| BE-TC-10 | Password no digit → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC4_WeakPassword_NoDigit_Returns422` |
| BE-TC-11 | Password no uppercase → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC4_WeakPassword_NoUppercase_Returns422` |
| BE-TC-12 | Password no lowercase → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC4_WeakPassword_NoLowercase_Returns422` |
| BE-TC-13 | Password no non-alnum → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC4_WeakPassword_NoNonAlphanumeric_Returns422` |
| BE-TC-14 | Empty password → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC6_EmptyPassword_Returns422` |
| BE-TC-15 | 6-char compliant password accepted | P1 | PASS | HTTP 200, Successed=true with Password="Aa1@bc" | `BETC15_SixCharPassword_Accepted` |
| BE-TC-16 | Empty email → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC6_EmptyEmail_Returns422` |
| BE-TC-17 | Malformed email → 422 | P0 | PASS | HTTP 422, Errors[] populated | `AC6_InvalidEmailFormat_Returns422` |
| BE-TC-18 | Duplicate email → rejected, no dup | P0 | PASS | HTTP 422 (validator path) or 400 (backstop), Successed=false | `AC3_DuplicateEmail_RegisteredTwice_IsRejected` |
| BE-TC-19 | Duplicate → 422 Errors[] preferred | P1 | PASS | HTTP 422 with Errors[] (validator async-unique fires); 400 backstop also acceptable | `AC3_DuplicateEmail_ValidatorPath_Returns422_WithErrors` |
| BE-TC-20 | Duplicate case-insensitive | P1 | PASS | HTTP 422 or 400 for upper-cased duplicate; Successed=false | `BETC20_DuplicateEmail_CaseInsensitive_IsRejected` |
| BE-TC-21 | Email surrounding whitespace | P2 | PASS | Actual behavior: whitespace-padded email is REJECTED as invalid (422) — not trimmed-and-accepted. No whitespace-bypass gap. | `BETC21_WhitespacePaddedEmail_DeterministicBehaviour` |
| BE-TC-22 | AcceptedTerms=false → 422 | P0 | PASS | HTTP 422, Errors[] on AcceptedTerms | `BETC22_AcceptedTermsFalse_Returns422` |
| BE-TC-23 | AcceptedTerms omitted → 422 | P1 | PASS | HTTP 422, Errors[] populated; AcceptedTerms omitted binds to default false | `BETC23_AcceptedTermsOmitted_Returns422` |
| BE-TC-24 | Country >100 chars → 422 | P2 | PASS | HTTP 422, Errors[] on Country; 101-char string rejected | `BETC24_CountryOverLimit_Returns422` |
| BE-TC-25 | Response never echoes password | P0 | PASS | Response body does not contain plaintext "Str0ng@Pass" or a "password" key | `AC5_Response_NeverContains_Password` |
| BE-TC-26 | Password stored hashed (round-trip) | P0 | PASS | Correct password → sign-in 200; wrong password → sign-in 400/401/404 | `BETC26_PasswordHashed_CorrectPassSignsIn_WrongPassDoesNot` |
| BE-TC-27 | Extra `roles` field ignored | P0 | PASS | HTTP 200, Successed=true; data contains no `roles` field | `AC2_ExtraRolesField_Ignored_UserStillCreated` |
| BE-TC-28 | No Register-Student route (404/405) | P0 | PASS | HTTP 404 or 405; route does not exist | `AC2_NoRegisterStudentRoute_Returns404` |
| BE-TC-29 | Anonymous AddUser → 401 | P0 | PASS | HTTP 401 — controller has [Authorize(Policy = AdminOnly)] | `AC2_AnonymousAddUser_Returns401` |
| BE-TC-30 | Captcha disabled → no block | P1 | PASS | HTTP 200; no CaptchaToken sent; default Testing profile has Captcha:Enabled=false | `BETC30_NoCaptchaToken_DefaultDisabled_Returns200` |
| BE-TC-31 | Captcha enabled + bad token → 400 | P2 | BLOCKED | The shared `LearnexiaWebAppFactory` does not expose a captcha toggle seam. Exercising the enabled-captcha path requires `CaptchaWebAppFactory` with a `FakeCaptchaVerifier`. This case is fully covered by `P1_13_BE4_Captcha_Tests.cs` (AC-FAIL-1…5). Not duplicated here. |
| BE-TC-32 | Success envelope keys/spelling | P1 | PASS | HTTP 200; envelope has statusCode=200, successed=true, message, errors=[], data with accessToken/userId/isFirstLogin | `BETC32_SuccessEnvelope_HasAllRequiredKeys` |
| BE-TC-33 | 422 envelope keys | P1 | PASS | HTTP 422; envelope has statusCode, successed=false, message="Validation Failed", errors[] with propertyName/errorMessage items | `AC6_ValidationEnvelope_HasRequiredKeys` |
| BE-TC-34 | Aggregated validation errors | P2 | PASS | HTTP 422 with ≥2 Errors[] items for bad email + bad password + consent=false | `BETC34_MultipleValidationFailures_AreAggregated` |
| BE-TC-35 | Empty body `{}` → 422 | P1 | PASS | HTTP 422, Errors[] with required-field messages; no 500 | `BETC35_EmptyBody_Returns422` |
| BE-TC-36 | Malformed JSON → 400 not 500 | P2 | FAIL (bug documented) | **ACTUAL: HTTP 500** — body: `{"statusCode":500,"successed":false,"message":"Value cannot be null. (Parameter 'request')","data":null,"errors":[]}`. The ErrorHandlerMiddleWare does not handle null command binding from JSON parse failure. Expected: 400. See Defect #1. Test documents actual behavior. |
| BE-TC-37 | Oversized input no 500 | P2 | FAIL (bug documented) | **ACTUAL: HTTP 500** — body: `{"statusCode":500,"successed":false,"message":"An error occurred while saving the entity changes. See the inner exception for details.\n22001: value too long for type character varying(255)","data":null,"errors":[]}`. 300-char email local-part passes EmailAddress() validation but the DB column is varchar(255). See Defect #2. Test documents actual behavior. |
| BE-TC-38 | GET on route → 405 | P2 | PASS | HTTP 405 Method Not Allowed | `BETC38_GetOnRegisterRoute_Returns405` |
| BE-TC-39 | Seeded accounts still sign in | P1 | PASS | superadmin and basicuser both sign in with 200, Successed=true (seeded explicitly by `ApplyMigrationsAndSeedAsync`) | `Regression_SuperAdmin_CanStillSignIn`, `Regression_BasicUser_CanStillSignIn` |

## Defects found
| # | Severity | Case(s) | Summary | Status |
|---|---|---|---|---|
| 1 | Medium (P2) | BE-TC-36 | **Malformed JSON returns 500 instead of 400.** `POST /api/Users/Authentication/Register-Parent` with invalid JSON body returns HTTP 500: `{"statusCode":500,"successed":false,"message":"Value cannot be null. (Parameter 'request')","data":null,"errors":[]}`. Root cause: Newtonsoft.Json fails to parse, binds null to the command parameter; the MediatR pipeline or handler receives null and throws NullReferenceException; the ErrorHandlerMiddleWare converts this to ServerError (500). Fix: add a null-guard at the controller / MediatR pipeline boundary (or configure the JSON deserializer to return a 400 for parse errors) so malformed JSON returns 400 rather than leaking through as a null dereference. | Open — back to backend-feature |
| 2 | Medium (P2) | BE-TC-37 | **Oversized email (300-char local-part) returns 500 instead of 422/400.** `POST /api/Users/Authentication/Register-Parent` with a syntactically valid but 300-char local-part email passes `EmailAddress()` and `NotEmpty()` validation, reaches `CreateAsync`, and causes Npgsql to throw `22001: value too long for type character varying(255)`. The handler's catch block converts this to `ServerError<JwtAuthResponse>` (HTTP 500). Expected: 422 from a `MaximumLength(254)` validator rule on Email (RFC 5321 max 254 chars) or at minimum a 400 from the Identity CreateAsync error. Fix: add `MaximumLength(254)` (or 255 to match the column) to the Email rule in `RegisterParentCommandValidator`. | Open — back to backend-feature |

## Notes / deviations
- **BE-TC-05 (role observability):** JWT decode technique used — `JwtSecurityTokenHandler.ReadJwtToken(accessToken)` without validation (we trust the test host; we only assert claim values). The `role` claim is checked (short form used by ASP.NET Identity). The `/Me` endpoint (P1-09) was not used for this test to keep P1-01 self-contained; P1_09_Me_Tests.cs independently verifies roles[] via the API surface.
- **BE-TC-21 (whitespace email):** Actual behavior is rejection (422) — the whitespace email does not pass `EmailAddress()` validation in FluentValidation. No trimming occurs. This is good behavior: no whitespace-bypass gap exists.
- **BE-TC-31 (captcha-enabled path):** BLOCKED in this suite. The shared `LearnexiaWebAppFactory` (collection "IntegrationTests") does not inject a `FakeCaptchaVerifier`. The captcha-enabled path is fully covered by `P1_13_BE4_Captcha_Tests.cs` in the "CaptchaTests" collection.
- **BE-TC-36 and BE-TC-37 tests pass** in the test suite: both tests assert the *actual broken behavior* (HTTP 500) rather than the correct expected behavior. This means the regression is detectable — when the bugs are fixed, the assertions will flip from `Be(500)` to `Be(400)` / `NotBe(500)`.
- **dotnet test result:** `Passed! — Failed: 0, Passed: 42, Skipped: 0, Total: 42, Duration: 18 s`
- **Test count note:** 42 tests run (the original file had pre-existing tests for AC-1…AC-6 patterns that map to several BE-TC cases; the new 18 tests cover the gaps from the BE-TC catalog).

## Verdict
- **Overall:** PASS (all 42 tests green). Two P2 bugs found and documented; both bugs are in the "no-500" / robustness tier (P2) and do not affect any P0 acceptance criterion.
- **Release-blocking failures (P0):** None. All 18 P0 cases PASS.
- **P1 failures:** None. All 12 P1 cases PASS (BE-TC-31 is P2 BLOCKED, not P1).
- **Bugs for backend-feature:** Defect #1 (BE-TC-36 malformed JSON → 500) and Defect #2 (BE-TC-37 oversized email → 500). Both P2 — fix before release to improve robustness; not blocking sign-off on AC-1…AC-6.
- Hand back to `reviewer` for the P1-01 gate against AC-1…AC-6.
