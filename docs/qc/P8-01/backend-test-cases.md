# P8-01 — Set a child's learning language · Backend test cases

> Story: [user-stories/Phase-8-Localization/P8-01-set-child-learning-language.md](../../../user-stories/Phase-8-Localization/P8-01-set-child-learning-language.md)
> Task: [tasks/Backend/Phase-8-Localization/P8-01-BE.md](../../../tasks/Backend/Phase-8-Localization/P8-01-BE.md)
> Module: **Identity / Parent** · Implemented by **api-tester** · Results → `execution-report.md`
> Mirror the established patterns in `P8_04_ChangeLearningLanguage_Tests.cs` (the `SendAsync` / `TryProp` / `CreateParentAndChildAsync` helpers, the `[Collection("IntegrationTests")]` + `IAsyncLifetime` shape, `ApplyMigrationsAndSeedAsync` + `LearningSeeder.SeedAsync`).

## Surface under test (mapped from the running code)
- **Set (initial):** `POST api/Parent/Add-Child` — body carries `LearningLanguage` ("ar"|"en"), **required**. Controller `[Authorize(Roles="Parent,Admin,SuperAdmin")]`. Acting parent resolved from JWT (no `ParentId` in body — IDOR-safe by construction). Validator: `AddChildCommandValidator` — `LearningLanguage` `NotEmpty` + `Must(== "ar" || == "en")` → 422 on violation. Persisted via `IChildAccountService.CreateChildAsync` (CreateChildRequest.LearningLanguage).
- **Read-back:** `GET api/Users/Me` — `MeResponse.LearningLanguage` (camelCase `learningLanguage`).
- **JWT claim:** `learning_language` emitted in `AuthenticationIdentityService.GetClaims` for the student token; re-issued on every `GenerateJwtToken` (initial + refresh).
- **Admin set:** `POST api/Admin/Users/{childId}/learning-language` exists but is the **destructive change** path (`AdminChangeLearningLanguageCommand`, confirm-gate, Math/Science reset) — this is **P8-04/P7-08 territory, NOT initial-set**. P8-01 "who can set it" = parent-at-onboarding only. Admin-as-setter is covered by the P8-04/P7-08 suite; do not duplicate here. (See coverage-report Overlap section.)

## Boundary vs P8-04 (avoid overlap)
- **P8-01 = INITIAL set at onboarding** via Add-Child + the persistence/claim/`/Me` plumbing.
- **P8-04 = LATER change** (parent-only, confirm-gate, Math/Science reset, integration event) — already covered by `P8_04_ChangeLearningLanguage_Tests.cs` (T1–T10 + Extras). **Do not re-test the change path here.** P8-01 does NOT publish `LearningLanguageChangedIntegrationEvent` (initial set only fires `UserRegisteredIntegrationEvent`).

## Decoding helpers
- Envelope: `successed` (bool), `statusCode` (int), `data`, `errors[]`, `message`. Use case-insensitive `TryProp`.
- A 422 body must have a non-empty `errors[]`; `successed=false`.

---

## Test cases

### BE-TC-01 — Add-Child with `learningLanguage="ar"` persists and is readable
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** migrations + identity seed applied. Register a parent (`POST api/Users/Authentication/Register-Parent`), capture parent token.
- **Steps:**
  1. `POST api/Parent/Add-Child` with parent token; body `{ FullName, Email(unique), Password=valid, Grade=1, Language="ar", Country="EG", LearningLanguage="ar" }`.
  2. Capture `data.id` (childId).
  3. Read child's `User.LearningLanguage` directly from `IdentityModuleDbContext` (mirror `GetChildLearningLanguageFromDbAsync`).
- **Expected:** Add-Child → 200/201, `successed=true`, `data.id>0`. DB `User.LearningLanguage == "ar"`.
- **Traces to:** AC "A child carries a LearningLanguage attribute"; AC "parent sets LearningLanguage when adding a child; required".

### BE-TC-02 — Add-Child with `learningLanguage="en"` persists `en`
- **Type:** functional / persistence / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** new parent token.
- **Steps:** Add-Child with `LearningLanguage="en"`; read DB.
- **Expected:** 200/201; DB `User.LearningLanguage == "en"` (both enum values exercised — proves it is not hard-coded to a default).
- **Traces to:** AC "LearningLanguage (ar|en)".

### BE-TC-03 — `LearningLanguage` is separate from `PreferredLanguage`
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** new parent token.
- **Steps:**
  1. Add-Child with `Language="ar"` (UI) **and** `LearningLanguage="en"` (medium) — deliberately divergent.
  2. Read `User.PreferredLanguage` and `User.LearningLanguage` from `IdentityModuleDbContext`.
- **Expected:** `PreferredLanguage == "ar"` and `LearningLanguage == "en"` — the two axes are stored independently and do not overwrite each other.
- **Traces to:** AC "LearningLanguage … separate from the UI PreferredLanguage".

### BE-TC-04 — `learningLanguage` omitted → 422 (required)
- **Type:** validation / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** new parent token.
- **Steps:** `POST api/Parent/Add-Child` with the `LearningLanguage` field **absent** (do not include the key); other fields valid.
- **Expected:** 422 UnprocessableEntity; `successed=false`; `errors[]` non-empty. **No child created** (assert: parent's child count via `GET api/Parent/My-Children` unchanged / empty).
- **Traces to:** AC "it is required".

### BE-TC-05 — `learningLanguage=""` (empty string) → 422
- **Type:** validation / boundary · **Priority:** P1 · **Target:** api-tester
- **Steps:** Add-Child with `LearningLanguage=""`; other fields valid.
- **Expected:** 422; `errors[]` non-empty; no child created.
- **Traces to:** AC "required".

### BE-TC-06 — invalid `learningLanguage="fr"` → 422
- **Type:** validation / negative · **Priority:** P0 · **Target:** api-tester
- **Steps:** Add-Child with `LearningLanguage="fr"`.
- **Expected:** 422; `successed=false`; `errors[]` non-empty (validator `Must(=="ar"||=="en")`). No child created.
- **Traces to:** AC "LearningLanguage (ar|en)" (valid value enforcement).

### BE-TC-07 — case-sensitivity boundary: `learningLanguage="AR"` → 422
- **Type:** boundary / negative · **Priority:** P1 · **Target:** api-tester
- **Steps:** Add-Child with `LearningLanguage="AR"` (uppercase).
- **Expected:** 422 — the validator matches the exact lowercase short codes only (`== "ar" || == "en"`); "AR" is rejected. Documents the strict-code contract so the seam never stores a non-canonical value.
- **Traces to:** AC "ar|en" (canonical-value contract). NOTE: if the implementation later normalizes case, flag as a defect-or-spec-change for the lead.

### BE-TC-08 — `/Me` returns `learningLanguage` for the child
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Add-Child with `LearningLanguage="en"`; sign in as the child (`POST api/Users/Authentication/Sign-In` with child email + password) to get a child token.
- **Steps:** `GET api/Users/Me` with the child token.
- **Expected:** 200; `data.learningLanguage == "en"`; field present (not null) for a student account.
- **Traces to:** AC "GET /Me … returns learningLanguage".

### BE-TC-09 — JWT carries the `learning_language` claim (verified via curriculum resolution)
- **Type:** functional / auth · **Priority:** P0 · **Target:** api-tester
- **Rationale:** the claim is internal; assert it **behaviorally** at the integration layer — the Learning read path reads it from the JWT (no query param), so a correct claim produces correct-language content.
- **Preconditions / seed:** `LearningSeeder.SeedAsync` run (bilingual trees). Add-Child with `LearningLanguage="en"`; sign in as child.
- **Steps:** `GET api/learning/Subjects/ForGrade?grade=1` with the child token. Inspect the returned MATH subject's resolved tree (e.g. its English display name, or cross-check the returned MATH subject id resolves to `Subject.Language == En` in `LearningDbContext`).
- **Expected:** 200; the MATH subject served is the **English** tree — proving the `learning_language="en"` claim was emitted and consumed. (For a child created with `LearningLanguage="ar"`, the MATH subject is the Arabic tree.)
- **Traces to:** AC "surfaced on the JWT (claim learning_language) so Learning resolves content language without a cross-module call".

### BE-TC-10 — refresh re-issues the `learning_language` claim
- **Type:** functional / auth · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Add-Child with `LearningLanguage="ar"`; sign in as child to obtain access + refresh tokens.
- **Steps:**
  1. Call the refresh endpoint (mirror the sign-in/refresh flow used elsewhere; resolve the actual route from `AuthenticationController`).
  2. Use the **refreshed** access token to call `GET api/learning/Subjects/ForGrade?grade=1`.
- **Expected:** 200; MATH subject served is the **Arabic** tree — the refreshed token still carries `learning_language="ar"`.
- **Traces to:** task BE-4 "ensure refresh re-issues it".
- **Blocker note:** if the refresh route/contract is not trivially callable in the test harness, mark **partially blocked** and assert re-issuance via re-sign-in instead (a fresh sign-in token must also carry the claim — same observable).

### BE-TC-11 — UI `PreferredLanguage` defaults to match `LearningLanguage` at onboarding (when UI language not divergently supplied)
- **Type:** functional · **Priority:** P2 · **Target:** api-tester
- **Rationale:** AC: "UI language defaults to match the chosen LearningLanguage at onboarding but remains independently editable." The Add-Child command currently takes both `Language` and `LearningLanguage` explicitly. Verify the **default-match** semantics as actually implemented.
- **Steps:**
  1. Add-Child supplying `LearningLanguage="en"`. (If the contract requires `Language` too, supply `Language="en"` to mirror the default; if the implementation derives `PreferredLanguage` from `LearningLanguage` when `Language` is absent, omit `Language`.)
  2. Read `User.PreferredLanguage` from DB.
- **Expected:** `PreferredLanguage` reflects the onboarding default = `en` (matches LearningLanguage when not divergently set).
- **Open question (lead):** is `Language` (UI) a *required* Add-Child field, or auto-defaulted from `LearningLanguage`? The validator currently requires `Language` (NotEmpty + ar|en). If required, this AC is satisfied by FE passing the same value — assert the stored equality only; flag the "auto-default when omitted" sub-clause as **not enforced server-side** (FE concern). See coverage-report gap G-01.

### BE-TC-12 — no student-facing endpoint mutates `LearningLanguage` (immutability by student)
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Rationale:** AC: "immutable by the student — there is no student-facing way to change it."
- **Preconditions / seed:** Add-Child `LearningLanguage="en"`; sign in as the child (Student-role token).
- **Steps:**
  1. With the **child (Student)** token, attempt `PUT api/Parent/Change-Learning-Language` (the change path) → expect **403** (role gate `Parent,Admin,SuperAdmin`). [This is the canonical "student cannot change" assertion.]
  2. With the **child** token, attempt `PATCH api/Admin/Users/{childId}/profile` and `POST api/Admin/Users/{childId}/learning-language` → expect **403** (AdminOnly).
  3. (If `UpdateChildProfile`/`Update-Child` accepts a `LearningLanguage` field, assert it is **ignored** — read DB after a child-self call to any reachable profile endpoint and confirm `LearningLanguage` unchanged.)
- **Expected:** every student-initiated mutation attempt is blocked (403) or silently ignores `LearningLanguage`; DB value unchanged.
- **Traces to:** AC "immutable by the student".

### BE-TC-13 — anonymous Add-Child → 401
- **Type:** auth · **Priority:** P1 · **Target:** api-tester
- **Steps:** `POST api/Parent/Add-Child` with a valid body but **no** Authorization header.
- **Expected:** 401 Unauthorized (no JWT). No child created.
- **Traces to:** parent-driven onboarding (only an authenticated parent can set it).

### BE-TC-14 — IDOR by construction: parent cannot target another family (no ParentId in body)
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Rationale:** Add-Child has no `ParentId` field; the child is auto-linked to the JWT caller. Confirm a child created by parent A is linked to A only.
- **Preconditions / seed:** parent A token, parent B token.
- **Steps:**
  1. Parent A: Add-Child → childId.
  2. Parent B: `GET api/Parent/My-Children` → assert childId is **not** in B's list.
  3. Parent A: `GET api/Parent/My-Children` → assert childId **is** in A's list.
- **Expected:** the new child belongs only to the acting (JWT) parent; the set is family-scoped — no cross-family injection.
- **Traces to:** product decision "parent-driven onboarding"; IDOR-safety of the set path. (Note: deeper cross-family change IDOR is P8-04 T3.)

### BE-TC-15 — duplicate email Add-Child → 400 (not 422), `LearningLanguage` not partially persisted
- **Type:** negative / persistence · **Priority:** P2 · **Target:** api-tester
- **Rationale:** ensures a failed create does not leave a half-set learning language (no orphan).
- **Steps:**
  1. Parent: Add-Child with email E and `LearningLanguage="ar"` → success.
  2. Same parent: Add-Child again with the **same** email E and `LearningLanguage="en"`.
- **Expected:** second call → 400 (DuplicateEmail mapped to BadRequest). Only **one** user exists for E; its `LearningLanguage` is still `"ar"` (the first set), not overwritten to `"en"`.
- **Traces to:** persistence integrity of the set path (supporting AC "required"/"persisted").

---

## Notes for the implementer
- Reuse `P8_04`'s `CreateParentAndChildAsync`, `SignInAndGetTokenAsync`, `GetChildLearningLanguageFromDbAsync`, `SendAsync`, `TryProp` verbatim — they already exist and are battle-tested.
- For BE-09/10, prefer asserting the claim **behaviorally** through `Subjects/ForGrade` rather than decoding the JWT, to keep the test resilient to token-format changes. If you do decode the JWT, the claim type constant is `learning_language` (`CustomClaimTypes.LearningLanguage`).
- The bilingual seed is **required** for BE-09/10/12-step-1 — call `LearningSeeder.SeedAsync(scope.ServiceProvider)` in `InitializeAsync`.
