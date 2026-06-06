# Backend Test Cases — P1-03 (Add-Child) — for `api-tester`

**Surface under test:** `POST /api/Parent/Add-Child` (Parent module, `ParentController`), plus interactions with `GET /api/Parent/My-Children` and `POST /api/Users/Authentication/Sign-In`.
**Auth gate:** `[Authorize(Roles = "Parent,Admin,SuperAdmin")]`.
**Envelope:** every response is `BaseResponse<T>` — assert `StatusCode`, `Successed` (note spelling), `Message`, and `Data` shape. The HTTP status returned by the controller is driven by `BaseResponse.StatusCode` via `NewResult(...)`.

## Request schema (actual, shipped code — use this exactly)
`AddChildCommand` JSON body:
```json
{
  "fullName":         "string (required)",
  "email":            "string (required, valid email — becomes UserName + login email)",
  "password":         "string (required, regex: >=1 lower, >=1 upper, >=1 digit, >=1 special, min len 6)",
  "grade":            1,            // int, required, InclusiveBetween(1,6)
  "language":         "ar",         // required, must be exactly "ar" or "en" (UI language)
  "country":          "EG",         // string, required, NotEmpty
  "learningLanguage": "ar"          // required, must be exactly "ar" or "en" (medium of instruction, P8-01)
}
```
`AddedChildResponse` (success `Data`): `{ id:int, fullName, email, grade:int?, language, country }`.

## Status-code map for this surface
| Outcome | HTTP | `Successed` | Source |
|---|---|---|---|
| Success | 200 | true | `Success(...)` |
| Shape-validation failure | 422 | false | `ValidationBehavior` (ICommand only) |
| Duplicate email | 400 | false | handler `BadRequest(ProfileDuplicateEmail)` |
| Missing/invalid JWT | 401 | (framework) | `[Authorize]` |
| Authenticated but wrong role | 403 | (framework) | role gate |
| Unhandled / create failure | 500 | false | handler `ServerError(SystemErrorSavingData)` |

## Shared preconditions / seed
- **P-Parent-A:** a registered Parent (via `POST /api/Users/Authentication/Register-Parent`) → obtain a valid Parent JWT. Use a fresh, unique email per run.
- **P-Parent-B:** a second registered Parent (different family) → Parent JWT. Used for cross-family checks.
- **P-Student:** a Student-role JWT (provision a child via P-Parent-A, then Sign-In as that child) for the role-gate negative cases.
- **Valid-Child payload:** a complete body per the schema above with a **unique** email and a policy-compliant password (e.g. `Aa1!aaaa`). Every "valid" case starts from this and mutates one field. **All seven fields including `learningLanguage` are required — a body missing `learningLanguage` will 422.**

---

## Functional — happy path (AC-1, AC-2, AC-3)

### BE-TC-01 — Parent adds a valid child → 200 + Student account created
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** P-Parent-A; Valid-Child payload with a unique email.
- **Steps:**
  1. `POST /api/Parent/Add-Child` with the Valid-Child payload and the Parent-A bearer token.
- **Expected:** HTTP **200**; `Successed == true`; `Data.id > 0`; `Data.fullName/email/grade/language/country` echo the request (language echoes the stored value); a `User` exists with the **Student** role (verify via My-Children / Sign-In, BE-TC-19 / BE-TC-22).
- **Traces to:** AC-1, AC-3.

### BE-TC-02 — Login email is the parent-assigned value (UserName = email)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** BE-TC-01 succeeded; record the email used.
- **Steps:**
  1. Confirm `Data.email` equals the email submitted.
  2. (Cross-check) the child can later authenticate with that exact email — see BE-TC-22.
- **Expected:** `Data.email` is byte-for-byte the submitted login email; no system-generated alias.
- **Traces to:** AC-3.

### BE-TC-03 — Grade / language / country persisted on the child profile
- **Type:** persistence · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** P-Parent-A.
- **Steps:**
  1. Add a child with `grade=4`, `language="en"`, `country="SA"`, `learningLanguage="ar"`.
  2. `GET /api/Parent/My-Children` with Parent-A token; find the child.
- **Expected:** `grade==4`; the listed `language` reflects the stored value (My-Children normalizes the stored culture code to short `"en"`/`"ar"`); `country=="SA"`; `learningLanguage` reflects the submitted medium-of-instruction value. Confirms the stored value is retrievable for downstream locale (AC-8) and Phase-3 prompt building.
- **Traces to:** AC-1, AC-8.

### BE-TC-04 — Parent adds two children in one session → both created and listed (AC-2, AC-5)
- **Type:** functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** P-Parent-A.
- **Steps:**
  1. Add child #1 (unique email E1) → expect 200.
  2. Add child #2 (different unique email E2) → expect 200.
  3. `GET /api/Parent/My-Children`.
- **Expected:** both 200; the two children have distinct `id`s and distinct emails; `My-Children` returns **both**, each linked to Parent-A.
- **Traces to:** AC-2, AC-5.

### BE-TC-05 — Adding a duplicate after a valid sibling does not undo the sibling (partial-failure safety) (AC-2, AC-7)
- **Type:** functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** P-Parent-A.
- **Steps:**
  1. Add child #1 (email E1) → 200.
  2. Add child #2 with the **same** email E1 → expect 400 duplicate.
  3. `GET /api/Parent/My-Children`.
- **Expected:** step 2 is 400; step 1's child still exists and is still listed — the duplicate failure created no second account and did not roll back the first.
- **Traces to:** AC-2, AC-7.

---

## Validation → 422 (AC-6)

> All cases below: P-Parent-A token, start from Valid-Child, mutate exactly one field. Expected envelope: HTTP **422**, `Successed == false`, `Message` is a localized human string (not a raw resource key), `Data` null/absent, and **no account created** (verify the email does not appear in My-Children and cannot Sign-In).

### BE-TC-06 — Grade below range (0) → 422
- **Type:** boundary · **Priority:** P0 · Mutate `grade=0`. Expected 422 (`GradeOutOfRange`). · **Traces to:** AC-6.

### BE-TC-07 — Grade above range (7) → 422
- **Type:** boundary · **Priority:** P0 · Mutate `grade=7`. Expected 422. · **Traces to:** AC-6.

### BE-TC-08 — Grade negative / extreme (-1 and 1000) → 422
- **Type:** boundary · **Priority:** P1 · Two sub-runs: `grade=-1`, `grade=1000`. Both 422. · **Traces to:** AC-6.

### BE-TC-09 — Empty password → 422
- **Type:** validation · **Priority:** P0 · Mutate `password=""`. Expected 422 (`LoginPasswordRequired`). · **Traces to:** AC-6.

### BE-TC-10 — Password fails complexity (too weak) → 422
- **Type:** validation · **Priority:** P0 · Sub-runs, each must 422 (`PasswordComplexityError`): `"alllower1!"` (no upper), `"ALLUPPER1!"` (no lower), `"Aa!aaaaa"` (no digit), `"Aa1aaaaa"` (no special), `"Aa1!"` (len < 6). · **Traces to:** AC-6.

### BE-TC-11 — Password meeting exactly the minimum complexity → 200 (positive boundary)
- **Type:** boundary · **Priority:** P1 · Mutate `password="Aa1!aa"` (len 6, all 4 classes). Expected **200** — confirms the regex is not over-strict. · **Traces to:** AC-6 (boundary).

### BE-TC-12 — `language` not in {ar,en} → 422
- **Type:** validation · **Priority:** P0 · Sub-runs: `language="fr"`, `language="EN"` (wrong case — must fail; rule is exact-match lowercase), `language=""`. All 422 (`InvalidLanguageCode` / `ProfileRequiredField`). · **Traces to:** AC-6.

### BE-TC-13 — `learningLanguage` missing or not in {ar,en} → 422
- **Type:** validation · **Priority:** P0 · Sub-runs: omit `learningLanguage` entirely; `learningLanguage="fr"`; `learningLanguage=""`. All 422. **This is the P8-01 field — easy to miss; assert it explicitly.** · **Traces to:** AC-6, Q-2.

### BE-TC-14 — Malformed email → 422
- **Type:** validation · **Priority:** P0 · Sub-runs: `email="not-an-email"`, `email="foo@"`, `email="@bar.com"`, `email=""`. All 422 (`ProfileInvalidEmailFormat` / `ProfileRequiredField`); no account created. · **Traces to:** AC-6.

### BE-TC-25 — Blank `fullName` → 422
- **Type:** validation · **Priority:** P1 · Mutate `fullName=""` (and a whitespace-only `"   "` sub-run). Expected 422 (`ProfileRequiredField`). · **Traces to:** AC-6.

### BE-TC-26 — Blank `country` → 422
- **Type:** validation · **Priority:** P1 · Mutate `country=""`. Expected 422 (`ProfileRequiredField`). · **Traces to:** AC-6.

---

## Auth / authz (AC-4)

### BE-TC-15 — No JWT → 401
- **Type:** auth-authz · **Priority:** P0 · **Steps:** `POST /api/Parent/Add-Child` with a valid body and **no** `Authorization` header. **Expected:** HTTP **401**; no account created. · **Traces to:** AC-4.

### BE-TC-16 — Expired / malformed JWT → 401
- **Type:** auth-authz · **Priority:** P1 · **Steps:** call with (a) a garbage bearer string, (b) an expired Parent token if obtainable. **Expected:** **401** for both; no account created. · **Traces to:** AC-4.

### BE-TC-17 — Student-role token → 403 (a child cannot add children)
- **Type:** auth-authz · **Priority:** P0 · **Preconditions:** P-Student JWT. **Steps:** call with a valid body and the Student bearer. **Expected:** HTTP **403**; no account created. Confirms the role gate and that a child has no path to provision accounts. · **Traces to:** AC-4.

### BE-TC-18 — Body cannot inject role or parentId (mass-assignment / privilege escalation)
- **Type:** auth-authz · **Priority:** P0 · **Preconditions:** P-Parent-A. **Steps:** send a valid body with **extra** fields the schema does not define: `"role":"Admin"`, `"roles":["SuperAdmin"]`, `"parentId":999999`, `"isStudent":false`. **Expected:** HTTP **200**; the created child is **Student** role only (verify via Sign-In → JWT roles claim) and is linked to **Parent-A** (the JWT id), **not** to `parentId=999999`. The extra fields are ignored — no privilege escalation, no cross-family link. · **Traces to:** AC-4, product override (no teacher role / server-assigned role).

### BE-TC-28 — There is no anonymous / child self-onboard path
- **Type:** regression / product-override · **Priority:** P1 · **Steps:** confirm `POST /api/Parent/Add-Child` requires auth (covered by BE-TC-15) AND that the only anonymous account-creation endpoint is `Register-Parent` (a parent), i.e. no anonymous "register child"/"self-onboard" route exists on the Parent or Auth controllers. **Expected:** add-child is auth-gated; no anonymous child-create endpoint is reachable. Encodes the "child cannot self-register/self-onboard" product decision. · **Traces to:** AC-4, story bullet "a child cannot self-register or self-onboard".

---

## Linkage & persistence (AC-5, AC-8)

### BE-TC-19 — Successful add auto-links child to the acting parent
- **Type:** persistence/linkage · **Priority:** P0 · **Preconditions:** P-Parent-A. **Steps:** add a child → `GET /api/Parent/My-Children`. **Expected:** the new child appears in Parent-A's list (a `ParentStudent` link row was created); link is to the **JWT-resolved** parent. · **Traces to:** AC-5.

### BE-TC-20 — Child created by Parent-A does NOT appear under Parent-B (family scope)
- **Type:** auth-authz/linkage · **Priority:** P0 · **Preconditions:** P-Parent-A, P-Parent-B. **Steps:** Parent-A adds a child → `GET /api/Parent/My-Children` with the **Parent-B** token. **Expected:** Parent-B's list does **not** contain Parent-A's child (no cross-family leakage). · **Traces to:** AC-5, AC-4 (IDOR).

### BE-TC-21 — Grade boundaries 1 and 6 persist correctly
- **Type:** boundary/persistence · **Priority:** P1 · **Steps:** add one child `grade=1`, another `grade=6` → verify both 200 and both grades persisted via My-Children. **Expected:** both accepted, grades stored as 1 and 6. · **Traces to:** AC-1, AC-6 (in-range boundary).

### BE-TC-22 — Created child can sign in with the assigned email + parent-set password (end-to-end)
- **Type:** persistence/functional · **Priority:** P0 · **Preconditions:** BE-TC-01 child (email + the password parent set). **Steps:** `POST /api/Users/Authentication/Sign-In` with that email + password. **Expected:** HTTP **200**; a JWT is issued whose roles claim is **Student** (and which carries the `learning_language` claim per P8-01). Confirms the assigned credential is real and the account is usable. · **Traces to:** AC-3, AC-8.

---

## Duplicate email — specific rejection (AC-7)

### BE-TC-23 — Duplicate login email → 400 with specific "email in use" message, no account
- **Type:** negative · **Priority:** P0 · **Preconditions:** P-Parent-A; an existing user with email E (e.g. a previously-added child, or the parent's own email). **Steps:** `POST /api/Parent/Add-Child` with `email=E` (rest valid). **Expected:** HTTP **400** (NOT 422); `Successed == false`; `Message` is the localized `ProfileDuplicateEmail` text (specific, not generic "bad request"); **no new account created** (My-Children count unchanged). · **Traces to:** AC-7.

### BE-TC-24 — Duplicate response is the same regardless of whose email it is (no cross-family leak)
- **Type:** negative/auth-authz · **Priority:** P1 · **Preconditions:** P-Parent-A; P-Parent-B has a child with email E_B; Parent-A has a child with email E_A; a parent account exists with email E_P. **Steps:** as Parent-A, attempt Add-Child with `email=E_B`, then `email=E_A`, then `email=E_P`. **Expected:** all three return the **same** 400 + `ProfileDuplicateEmail` message — the response does not reveal whether the email belongs to your own child, a foreign family's child, or a parent (no enumeration of cross-family relationships). · **Traces to:** AC-7, Q-3 (security-auditor confirms).

### BE-TC-27 — Role-assign failure triggers compensating delete (no orphaned half-created account) — PARTIALLY BLOCKED
- **Type:** negative/persistence · **Priority:** P2 · **Preconditions:** a fault-injection hook on `IChildAccountService.CreateChildAsync` to force `AddToRoleAsync` failure (the seam documents a compensating `DeleteAsync` on role-assign failure). **Steps:** force role-assign failure during an Add-Child; then attempt Sign-In with that email and re-attempt Add-Child with the same email. **Expected:** Add-Child returns 500 (`SystemErrorSavingData`); **no** user remains (Sign-In fails; the email is free to reuse — no `DuplicateEmail` 400 on retry). **Blocker:** not triggerable from the pure HTTP surface without a fault seam — `api-tester` to mark BLOCKED with this reason if no hook exists, and report the gap. · **Traces to:** AC-1 (no partial account on failure), Q-6.

---

## P2 / nice-to-have

### BE-TC-12b — `country` whitespace-only — DOCUMENT BEHAVIOR
- **Type:** validation · **Priority:** P2 · `country="   "`. **Expected:** `NotEmpty()` in FluentValidation treats whitespace-only as empty for strings → 422. Confirm and record actual behavior. · **Traces to:** AC-6.

### BE-TC-29 — Admin/SuperAdmin token can call Add-Child (support flow)
- **Type:** auth-authz · **Priority:** P2 · **Preconditions:** an Admin JWT. **Steps:** call Add-Child with a valid body. **Expected:** **200** (the gate allows `Parent,Admin,SuperAdmin`); confirm the child is created and linked to the **acting Admin's** id (JWT-resolved) — document this support-flow behavior. · **Traces to:** AC-4 (gate composition).

### BE-TC-30 — Oversized inputs are handled gracefully (no 500)
- **Type:** boundary · **Priority:** P2 · **Steps:** submit a `fullName` of ~10,000 chars and a very long `country`. **Expected:** either a clean 422/400 or a 200 with truncation per DB column limits — but **never an unhandled 500**. Record actual behavior (informs whether a max-length rule is missing). · **Traces to:** AC-6 (robustness).

---

## Notes for `api-tester`
- Assert the envelope `Successed` spelling exactly (`Successed`, not `Successful`).
- For every "no account created" expectation, verify via `GET /api/Parent/My-Children` count delta **and** a failed Sign-In — do not rely on the response alone.
- Use a unique email per test run to keep cases independent and re-runnable.
- Flag any **unexpected 500** as a defect (the only intended 500 is the forced-failure path BE-TC-27).
- If a happy-path case unexpectedly 422s, first confirm the body includes all seven fields including `learningLanguage` (the most common cause).
