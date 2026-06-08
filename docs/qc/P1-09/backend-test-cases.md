# P1-09 — Backend Test Cases (for `api-tester`)

> Surface under test (P1-09-distinct): **`GET /api/Users/Me`**
> `backend/src/Modules/Identity/Learnexia.Modules.Identity.Api/Controllers/UsersController.cs`
> Handler: `…/Application/Features/Authentications/Queries/GetMe/GetMeQueryHandler.cs`
> DTO: `…/GetMe/MeResponse.cs` · plus the **child Sign-In → Me locale chain** (BE-2).
>
> **Baseline:** an integration-test file already exists — `backend/tests/Learnexia.IntegrationTests/P1_09_Me_Tests.cs` (~23 facts).
> **Extend that file**; reuse its helpers (`SendAsync`, `TryProp`, `RegisterParentAsync`, `SignInAndGetTokenAsync`, `AddChildAsync`, `GetMeAsync`).
> Cases marked **[EXISTING]** already have a fact in that file — keep/verify, do not duplicate. Cases marked **[NEW]** must be added.

## Contract reference (live `MeResponse`)
`BaseResponse<MeResponse>` — success flag spelled **`Successed`**. `MeResponse` fields:
`id (int), roles (string[], PascalCase e.g. "Parent"/"Student"), fullName (string?), preferredLanguage (string?),
learningLanguage (string?), isFirstLogin (bool), hasChildren (bool), phone (string?), country (string?),
avatarUrl (string?), grade (int?)`. Controller serializes camelCase (Newtonsoft); the 401/middleware path may
serialize PascalCase — use the existing `TryProp` case-insensitive lookup for all assertions.

## NOT in scope here (cross-reference only — do NOT re-implement)
- `POST …/Register-Parent`, `POST …/Sign-In`, `POST …/Sign-Out`, `POST …/Refresh-Token` → **P1-01 / P1-02** tests.
- `POST /api/Parent/Add-Child`, `POST /api/Parent/Link-Child`, `GET /api/Parent/My-Children` → **P1-03 / P1-04** tests.
  (These are used here only as **seed/setup** for `Me` cases, not as cases themselves.)

---

## Group A — Auth gate on `Me` (must require a valid JWT)

### BE-TC-01 — `Me` with no token → 401  **[EXISTING]**
- **Type:** auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** none.
- **Steps:** 1. `GET /api/Users/Me` with no `Authorization` header.
- **Expected:** HTTP **401 Unauthorized**. No `MeResponse` body leak (no `id`/`roles`).
- **Traces to:** BE-1 "Me requires auth".

### BE-TC-02 — `Me` with a malformed/garbage token → 401  **[EXISTING]**
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** none.
- **Steps:** 1. `GET /api/Users/Me` with `Authorization: Bearer this.is.not.a.valid.jwt`.
- **Expected:** HTTP **401 Unauthorized**.
- **Traces to:** BE-1 "Me requires auth".

---

## Group B — Self-scoping / no-IDOR (reads only from the JWT)

### BE-TC-03 — `Me` returns the caller's own id, not another user's  **[EXISTING]**
- **Type:** auth-authz / functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register two distinct parents A and B (capture each `userId` + token).
- **Steps:** 1. `GET /Me` with token A. 2. `GET /Me` with token B.
- **Expected:** 200 each; `data.id` for A == A's userId; for B == B's userId; the two ids differ.
- **Traces to:** BE-1 "self-scoped".

### BE-TC-04 — `?userId=<other-id>` query param is ignored  **[EXISTING]**
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register parents A and B.
- **Steps:** 1. With token A, `GET /api/Users/Me?userId=<B's id>`.
- **Expected:** 200; `data.id` == A's id (the crafted query param is not model-bound; no IDOR surface).
- **Traces to:** BE-1 "no IDOR".

---

## Group C — Role projection (drives FE routing)

### BE-TC-05 — Fresh parent → `Me` 200 with full `BaseResponse` envelope + `roles` includes "Parent"  **[EXISTING]**
- **Type:** functional / state · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register one parent.
- **Steps:** 1. `GET /Me` with the parent token.
- **Expected:** HTTP **200**. Envelope has `statusCode=200`, **`successed`** (exact spelling) `=true`,
  `message`, `errors`, and non-null `data`. `data.roles` is a JSON array containing `"Parent"`
  (case-insensitive). Confirm `data.roles` is an array even with a single role.
- **Traces to:** BE-1 "Me returns role"; envelope/`Successed` contract.

### BE-TC-06 — Seeded superadmin → `roles` includes "Admin" and "SuperAdmin"  **[EXISTING]**
- **Type:** auth-authz / functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Sign in as seeded `superadmin` / `123Pa$$word!`.
- **Steps:** 1. `GET /Me` with the admin token.
- **Expected:** 200; `data.roles` contains both `"SuperAdmin"` and `"Admin"` (case-insensitive).
- **Traces to:** BE-1 "Me returns role".

### BE-TC-07 — Superadmin `Me.id` ≠ a parent's id (role + scope sanity)  **[EXISTING]**
- **Type:** auth-authz · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; sign in as superadmin.
- **Steps:** 1. `GET /Me` as admin; 2. compare `data.id` to the parent's id.
- **Expected:** 200; admin `data.id` ≠ parent id.
- **Traces to:** BE-1 "self-scoped".

---

## Group D — Language projection (drives locale / RTL)

### BE-TC-08 — Parent `Me.preferredLanguage` is a valid locale (or documented fallback)  **[NEW]**
- **Type:** functional / validation · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register one parent (no explicit UI language chosen).
- **Steps:** 1. `GET /Me` with the parent token; read `data.preferredLanguage`.
- **Expected:** 200; the `preferredLanguage` key is **present**. Its value is either a valid locale
  string in `{"ar","en"}` **or** `null` — assert against the value the lead confirms (see README Open
  Q3). The field must never be absent.
- **Traces to:** BE-1 "Me returns language".

### BE-TC-09 — Fresh parent → `isFirstLogin = true` (onboarding not complete)  **[EXISTING]**
- **Type:** functional / state · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register one parent (so `RegistrationIsCompleted=false`).
- **Steps:** 1. `GET /Me`.
- **Expected:** 200; `data.isFirstLogin == true`.
- **Traces to:** BE-1 "onboarding flag".

### BE-TC-10 — `Me.hasChildren` reflects linkage state (false fresh → true after Add-Child; B stays false)  **[EXISTING]**
- **Type:** persistence / state · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register parent A; register parent B.
- **Steps:** 1. `GET /Me` as A before any child → `hasChildren=false`. 2. `POST /api/Parent/Add-Child`
  as A (valid child). 3. `GET /Me` as A → `hasChildren=true`. 4. `GET /Me` as B → still `false`.
- **Expected:** Each assertion holds; `hasChildren` is scoped strictly to the calling parent (no
  cross-parent bleed). Add-Child here is **seed**, not a case under test.
- **Traces to:** BE-1 "onboarding flag (has-children signal)"; self-scoping.

---

## Group E — Child login → `Me` home-context chain (BE-2, the P1-09-distinct behavior)

> These cases verify the *data* behind AC-4: a child signs in with the parent-assigned email and the
> app learns its language / RTL / grade from `Me`. `LinkedChildResponse` omits language by design, so
> `Me` is the only source. **Sign-In itself is P1-01/P1-02-owned — it is seed here, not the case.**

### BE-TC-11 — Child Sign-In → `Me.preferredLanguage` equals the language set at Add-Child  **[NEW]**
- **Type:** functional / persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; `Add-Child` with `Language="ar"` (and a known email/password).
- **Steps:** 1. `POST /Sign-In` as the child (parent-assigned email + password) → capture child token.
  2. `GET /Me` with the child token.
- **Expected:** 200; `data.preferredLanguage == "ar"` (the value set at Add-Child) — this is the field
  the FE uses to set locale + RTL on child login. Repeat-or-parametrize with `Language="en"` to confirm
  both locales round-trip.
- **Traces to:** AC-4 (child lands on home in chosen language / RTL for Arabic).

### BE-TC-12 — Child Sign-In → `Me.learningLanguage` equals the value set at Add-Child  **[EXISTING]**
- **Type:** functional / persistence · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; `Add-Child` with `LearningLanguage="en"`.
- **Steps:** 1. Sign in as child → token. 2. `GET /Me`.
- **Expected:** 200; `data.learningLanguage == "en"`. (P8-01 field; asserted at the P1-09 chain level so
  the child's medium-of-instruction is available to the app post-login.)
- **Traces to:** AC-4 (child home context).

### BE-TC-13 — Child Sign-In → `Me.grade` equals the grade set at Add-Child; parent `Me.grade` is null  **[EXISTING]**
- **Type:** functional / boundary · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; `Add-Child` with `Grade=3`.
- **Steps:** 1. Sign in as child → token; `GET /Me` → assert `data.grade == 3` (number).
  2. `GET /Me` as the parent → assert `data.grade` is `null`.
- **Expected:** Both hold. (Grade drives the child's subject-scoping on the home; null for non-students.)
- **Traces to:** AC-4 (child home context); product (grade 1–6).

### BE-TC-14 — Child `Me.roles` contains "Student" and NOT "Parent"  **[NEW]**
- **Type:** auth-authz / functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; `Add-Child`.
- **Steps:** 1. Sign in as the child → token. 2. `GET /Me`.
- **Expected:** 200; `data.roles` contains `"Student"` (case-insensitive) and does **not** contain
  `"Parent"`/`"Admin"`/`"SuperAdmin"`. This is what routes the child to the child home rather than
  onboarding/parent surfaces. Also assert `data.hasChildren == false` for the child.
- **Traces to:** AC-4 (child routes to own home); BE-1 "role drives routing".

---

## Group F — Response safety & field-set integrity

### BE-TC-15 — `Me` body leaks no sensitive internal fields  **[EXISTING]**
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions / seed:** Register a parent with a known password.
- **Steps:** 1. `GET /Me`; inspect the raw body string.
- **Expected:** 200; body contains **none** of: the plaintext password, `passwordHash`, `securityStamp`,
  `concurrencyStamp`, `normalizedEmail`, `normalizedUserName`, `lockoutEnd`, refresh-token strings.
- **Traces to:** BE-1 "no sensitive fields"; overlaps security-auditor Gate 0A.

### BE-TC-16 — `Me.data` exposes the full P1-09 routing field set  **[NEW]**
- **Type:** functional / regression · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register a parent with a `fullName`.
- **Steps:** 1. `GET /Me`; assert each key is present in `data`.
- **Expected:** 200; `data` contains **all** routing-relevant keys: `id, roles, fullName,
  preferredLanguage, isFirstLogin, hasChildren, grade` (present even when null/false). Assert `id>0`,
  `roles` is a non-empty array, `isFirstLogin`/`hasChildren` are booleans, and `fullName` equals the
  registered name. **Tolerant of additive fields** (`learningLanguage/phone/country/avatarUrl`) so later
  stories don't break this case.
- **Traces to:** BE-1 envelope/contract; `Successed` spelling (assert in BE-TC-05).

### BE-TC-17 — `Me` token lifecycle: refreshed token authorizes; post-sign-out behavior confirmed  **[NEW]**
- **Type:** auth-authz / state · **Priority:** P1 · **Target:** api-tester
- **Preconditions / seed:** Register a parent → capture access + refresh token.
- **Steps (leg 1 — refresh):** 1. `POST …/Refresh-Token` with `{accessToken, refreshToken}` → new access
  token. 2. `GET /Me` with the **new** access token.
- **Expected (leg 1):** 200; `data.id` is the same parent. A refreshed token authorizes `Me`.
- **Steps (leg 2 — sign-out):** 3. `POST …/Sign-Out` with the current access token. 4. `GET /Me` with
  that same (now signed-out) access token.
- **Expected (leg 2):** assert the **documented** behavior (see README Open Q2): either **401**
  (session-revocation middleware rejects the revoked token) or **200-until-JWT-expiry**. Mark this leg
  **confirm-expected** — `api-tester` confirms the intended status with the lead and asserts that one;
  do not silently accept either.
- **Traces to:** BE-1 robustness; refresh-token chain (P1-02 seed); sign-out (P1-02 seed).

### BE-TC-18 — No "Teacher" role is ever returned (product override)  **[NEW]**
- **Type:** negative / regression · **Priority:** P2 · **Target:** api-tester
- **Preconditions / seed:** Register a parent; `Add-Child`; sign in as the child; sign in as superadmin.
- **Steps:** 1. `GET /Me` for parent, child, and admin tokens; collect all `data.roles`.
- **Expected:** Across all three, no role equals `"Teacher"` (case-insensitive). Asserts the
  no-teacher-role product decision at the projection boundary.
- **Traces to:** Product override "no teacher role".

---

## Implementation notes for `api-tester`
- **Extend** `P1_09_Me_Tests.cs`; do not fork a new file. The **[EXISTING]** cases are already there —
  verify they still pass and map them to these IDs in `execution-report.md`. Implement the **[NEW]**
  cases (BE-TC-08, 11, 14, 16, 17, 18) using the same helpers.
- Use the case-insensitive `TryProp` for every field read (two serialization paths exist).
- Seed all data via the API (`Register-Parent`, `Add-Child`, `Sign-In`, `Refresh-Token`, `Sign-Out`);
  unique emails via the existing `UniqueEmail` helper. Do not seed the DB directly.
- For BE-TC-08 and BE-TC-17-leg-2, get the expected value/status from the lead (README Open Q2/Q3)
  before locking the assertion. If unresolved at run time, mark the case **Blocked** in the report with
  the open question, not a guessed pass.
- Do **not** add cases for `Register-Parent`/`Sign-In`/`Add-Child`/`Link-Child`/`My-Children`/`Sign-Out`
  *as endpoints* — they are P1-01/02/03/04-owned and appear here only as seed/setup.
