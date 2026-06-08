# P1-01 — Backend Test Cases (Register as a parent)

**Target agent:** `api-tester`
**Surface under test:** `POST /api/Users/Authentication/Register-Parent` — `[AllowAnonymous]`
**Module:** Identity (`Learnexia.Modules.Identity.*`)
**Implementation refs (load-bearing):**
- Controller: `backend/src/Modules/Identity/Learnexia.Modules.Identity.Api/Controllers/AuthenticationController.cs`
- Command: `…/Application/Features/Authentications/Commands/RegisterParent/RegisterParentCommand.cs`
- Handler: `…/RegisterParent/RegisterParentCommandHandler.cs`
- Validator: `…/Application/Features/Authentications/Validation/RegisterParentCommandValidator.cs`
- Envelope: `backend/src/Shared/Learnexia.Shared.Kernel/Responses/BaseResponse.cs`
- Response DTO: `…/Identity.Domain/Helpers/JwtAuthResponse.cs`
- 422 path: `backend/src/Host/Learnexia.Host/Middleware/ErrorHandlerMiddleWare.cs`

---

## Contract facts the implementer must assert against

### Request body (`RegisterParentCommand`)
| Field | Type | Required | Notes |
|---|---|---|---|
| `Email` | string | yes | NotEmpty + `EmailAddress()` + async-unique. Becomes `UserName`. |
| `Password` | string | yes | NotEmpty + regex `^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$` (min 6; lower+upper+digit+non-alnum). |
| `FullName` | string? | no | Defaults to email local-part when blank. |
| `Country` | string? | no | Optional; max length 100; stored on `User.Nationality`. |
| `AcceptedTerms` | bool | **yes — must be `true`** | Validator `Equal(true)`. Handler stamps `AcceptedTermsAtUtc`. |
| `CaptchaToken` | string? | no | Verified by `ICaptchaVerifier`. Default config `Captcha:Enabled=false` → no-op pass-through. |

There is **no** `Roles`/`Role` field — role is server-assigned (`Roles.Parent`).

### Success envelope (`BaseResponse<JwtAuthResponse>`, HTTP 200)
`Successed=true`, `StatusCode=200`, `Message`, `Errors=[]`, and `Data` =
`{ AccessToken (non-empty string), refreshToken, UserId (>0), IsFirstLogin (true on register), SessionTimeout, SessionId }`.
**No** `password`/`roles` key anywhere in the body.

### Failure envelopes
- **Validation (422):** thrown `ValidationException` → middleware → `Successed=false`, `StatusCode=422`, `Message="Validation Failed"`, `Errors[]` of `{ propertyName, errorMessage }`. *(Middleware now serialises camelCase, matching the controller path. Use a case-insensitive property lookup to stay robust.)*
- **Business rule (400):** handler `BadRequest<JwtAuthResponse>(...)` → `Successed=false`, `StatusCode=400`, `Message` populated, `Errors=[]` (duplicate-email race backstop; captcha failure when enabled).
- **Server error (500):** handler `ServerError<…>(…)` → generic localized message, no internals leaked.

### Seed / harness notes
- Use a freshly registered parent as the "existing account" for duplicate-email cases. The **Testing** environment does **not** seed legacy `superadmin`/`basicuser` for collision purposes (per the existing P1-01 test file), so create the collision target yourself.
- Generate a unique email per test run (timestamp + GUID) to keep cases independent and re-runnable.
- A strong baseline password that passes every rule: `Str0ng@Pass`.

---

## Case schema
ID · Title · Type · Priority · Target agent · Preconditions/seed · Steps · Expected · Traces to.
All cases target `api-tester`. Priorities: P0 blocks release, P1 should, P2 nice.

---

## Group A — Happy path (functional / persistence)

### BE-TC-01 — Valid registration returns 200 + JWT envelope
- **Type:** functional · **Priority:** P0
- **Preconditions:** none; unique email.
- **Steps:**
  1. `POST /api/Users/Authentication/Register-Parent` with `{ Email: <unique>, Password: "Str0ng@Pass", AcceptedTerms: true }`.
- **Expected:** HTTP `200`; `Successed=true`; `Data.AccessToken` is a non-empty string; `StatusCode=200`; `Errors=[]`.
- **Traces to:** AC-1 (story: "valid email+password → account created and JWT returned").

### BE-TC-02 — Fresh registration sets `IsFirstLogin=true`
- **Type:** functional · **Priority:** P1
- **Preconditions:** unique email.
- **Steps:** Register as in BE-TC-01.
- **Expected:** HTTP `200`; `Data.IsFirstLogin == true`.
- **Traces to:** AC-1; brief AC-1 (`IsFirstLogin=true` parity with SignIn).

### BE-TC-03 — Successful registration returns a non-zero `UserId`
- **Type:** persistence · **Priority:** P1
- **Preconditions:** unique email.
- **Steps:** Register as in BE-TC-01.
- **Expected:** HTTP `200`; `Data.UserId > 0` (proves the user was persisted).
- **Traces to:** AC-1.

### BE-TC-04 — Round-trip: registered parent can immediately sign in
- **Type:** persistence / functional · **Priority:** P0
- **Preconditions:** unique email `E`, password `Str0ng@Pass`.
- **Steps:**
  1. Register `{ Email: E, Password, AcceptedTerms: true }` → expect 200.
  2. `POST /api/Users/Authentication/Sign-In` with `{ UserName: E, Password }`.
- **Expected:** Sign-in returns HTTP `200`, `Successed=true`, non-empty `Data.AccessToken`. Proves the password was hashed correctly and the account (with `UserName==Email`) persisted and is retrievable.
- **Traces to:** AC-1; AC-5 (hashing is functional, not corrupting).

### BE-TC-05 — Registered parent holds **only** the Parent role
- **Type:** auth-authz / persistence · **Priority:** P0
- **Preconditions:** register a fresh parent `E`, capture its access token.
- **Steps:**
  1. Register `E` → 200, capture `AccessToken`.
  2. Call an authenticated endpoint that exposes the caller's roles/claims (e.g. `GET /api/Users/.../Me`) or decode the JWT `role` claims from `AccessToken`.
- **Expected:** the user is in role `Parent` and in **no** other role (no `Student`, `Admin`, `SuperAdmin`). Role is server-assigned.
- **Traces to:** AC-1 ("a parent account is created"); brief AC-2.
- **Note:** if no role-exposing authenticated endpoint is reachable from the harness, decode the JWT and assert the `role`/`roles` claim set. Document the method used.

### BE-TC-06 — `FullName` omitted → defaults to email local-part, no 500
- **Type:** boundary · **Priority:** P1
- **Preconditions:** unique email `alice@…`.
- **Steps:** Register `{ Email, Password: "Str0ng@Pass", AcceptedTerms: true }` (no `FullName`).
- **Expected:** HTTP `200`, `Successed=true` (no 500). Account created with a non-null name.
- **Traces to:** AC-1; brief Q1 resolution / plan BE-1b.

### BE-TC-07 — `FullName` provided is accepted
- **Type:** functional · **Priority:** P2
- **Steps:** Register with `FullName: "Test Parent"` + valid fields.
- **Expected:** HTTP `200`, `Successed=true`.
- **Traces to:** AC-1.

### BE-TC-08 — Optional `Country` accepted and persisted
- **Type:** functional / persistence · **Priority:** P2
- **Steps:** Register with `Country: "Egypt"` + valid fields.
- **Expected:** HTTP `200`, `Successed=true`. (If a profile/`Me` endpoint exposes nationality, assert it round-trips; otherwise assert no error.)
- **Traces to:** BE-9 extension on this endpoint (not an original AC — covered as no-regression).

---

## Group B — Password policy (validation → 422)

> All Group-B cases expect HTTP **422**, `Successed=false`, non-empty `Errors[]` whose items each carry `propertyName` and `errorMessage`, and **no account created**.

### BE-TC-09 — Password too short (<6) → 422
- **Type:** validation/boundary · **Priority:** P0
- **Steps:** Register with `Password: "Ab1@x"` (5 chars), valid email, `AcceptedTerms: true`.
- **Expected:** 422; `Errors[]` populated on `Password`.
- **Traces to:** AC-4 ("password fails strength rules → blocked with specific message"); AC-6.

### BE-TC-10 — Password missing a digit → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Password: "NoDigit@Pass"`.
- **Expected:** 422; `Errors[]` populated.
- **Traces to:** AC-4, AC-6.

### BE-TC-11 — Password missing uppercase → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Password: "nouppercase1@"`.
- **Expected:** 422; `Errors[]` populated.
- **Traces to:** AC-4, AC-6.

### BE-TC-12 — Password missing lowercase → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Password: "NOLOWER1@PASS"`.
- **Expected:** 422; `Errors[]` populated.
- **Traces to:** AC-4, AC-6.

### BE-TC-13 — Password missing non-alphanumeric → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Password: "NoSpecial1Pass"`.
- **Expected:** 422; `Errors[]` populated.
- **Traces to:** AC-4, AC-6.

### BE-TC-14 — Empty password → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Password: ""`, valid email, `AcceptedTerms: true`.
- **Expected:** 422; `Errors[]` populated on `Password`.
- **Traces to:** AC-4, AC-6.

### BE-TC-15 — Boundary: exactly-6-char compliant password is **accepted**
- **Type:** boundary · **Priority:** P1
- **Steps:** `Password: "Aa1@bc"` (6 chars, all rules satisfied).
- **Expected:** HTTP `200`, `Successed=true`. Confirms the `.{6,}` boundary admits the minimum-length valid password (no off-by-one over-rejection).
- **Traces to:** AC-4 (allowed boundary; negative side covered by BE-TC-09).

---

## Group C — Email validation & duplicate (validation → 422 / business → 400)

### BE-TC-16 — Empty email → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Email: ""`, valid password, `AcceptedTerms: true`.
- **Expected:** 422; `Errors[]` populated on `Email`.
- **Traces to:** AC-6.

### BE-TC-17 — Malformed email → 422
- **Type:** validation · **Priority:** P0
- **Steps:** `Email: "not-an-email"`.
- **Expected:** 422; `Errors[]` populated on `Email`.
- **Traces to:** AC-6.

### BE-TC-18 — Duplicate email (register same address twice) → rejected, no second account
- **Type:** negative / persistence · **Priority:** P0
- **Preconditions:** register email `E` once (200).
- **Steps:** `POST` again with the same `E` + valid fields.
- **Expected:** status is **422** (validator async-unique rule, preferred) **or** **400** (handler backstop); `Successed=false`. No second account is created (verify: a sign-in with `E` still maps to the original single user; count not duplicated).
- **Traces to:** AC-3 ("already registered → clear error, no duplicate account created"); AC-6.

### BE-TC-19 — Duplicate email surfaces the validator path with `Errors[]` (preferred shape)
- **Type:** validation · **Priority:** P1
- **Preconditions:** register `E` once.
- **Steps:** re-`POST` `E`.
- **Expected:** **If 422** — `Errors[]` non-empty, item on `Email` with a human-readable duplicate message (not a raw key). **If 400** — accept as backstop but flag that the FE-preferred 422 path was not taken (note in execution report). Either way `Successed=false`.
- **Traces to:** AC-3, AC-6; brief Q5 (error-shape consistency).

### BE-TC-20 — Duplicate email is case-insensitive (collision on differing case)
- **Type:** negative/boundary · **Priority:** P1
- **Preconditions:** register `user@example.com`.
- **Steps:** register `USER@EXAMPLE.COM` (same address, different case).
- **Expected:** rejected (422 or 400), `Successed=false`, no second account. (ASP.NET Identity normalizes email/username; assert the guard isn't case-bypassable — a known enumeration/duplication hole if it is.)
- **Traces to:** AC-3.

### BE-TC-21 — Email with surrounding whitespace
- **Type:** boundary · **Priority:** P2
- **Steps:** register `"  spaced@example.com  "`.
- **Expected:** deterministic, documented behaviour — **either** rejected as invalid (422) **or** trimmed-and-accepted; if accepted, a second registration of the trimmed form must then be rejected as duplicate (no whitespace-padding bypass of the uniqueness guard). Record actual behaviour.
- **Traces to:** AC-3 (edge of the uniqueness guard).

---

## Group D — Terms consent (validation → 422)

### BE-TC-22 — `AcceptedTerms=false` → 422
- **Type:** validation · **Priority:** P0
- **Steps:** register with valid email+password but `AcceptedTerms: false`.
- **Expected:** 422; `Errors[]` populated on `AcceptedTerms`; no account created.
- **Traces to:** BE-9 consent requirement (COPPA audit) — not an original story AC; covered as a hard gate.

### BE-TC-23 — `AcceptedTerms` omitted → 422
- **Type:** validation/boundary · **Priority:** P1
- **Steps:** register with valid email+password, no `AcceptedTerms` key (binds to default `false`).
- **Expected:** 422; `Errors[]` populated on `AcceptedTerms`.
- **Traces to:** BE-9 consent requirement.

---

## Group E — Country bound (validation → 422)

### BE-TC-24 — Over-long `Country` (>100 chars) → 422
- **Type:** boundary/validation · **Priority:** P2
- **Steps:** register with `Country` = 101-char string + otherwise valid fields.
- **Expected:** 422; `Errors[]` populated on `Country`.
- **Traces to:** BE-9 country length bound.

---

## Group F — Password hygiene (security)

### BE-TC-25 — Response body never echoes the password
- **Type:** security (negative) · **Priority:** P0
- **Steps:** register with a known password `P`; capture full response body string.
- **Expected:** the raw body contains neither the literal `P` nor any `"password"` key (case-insensitive). On the success path **and** on a failure path (re-check with a 422 response).
- **Traces to:** AC-5 ("response never returns the password").

### BE-TC-26 — Password is stored hashed, not plaintext (indirect assertion)
- **Type:** security / persistence · **Priority:** P0
- **Steps:** register `E`/`P`; then sign in with `E`/`P` (succeeds) and sign in with `E`/`WrongPass` (fails).
- **Expected:** correct password authenticates, wrong password does not → consistent with a salted hash store (Identity). If the harness allows direct DB inspection, additionally assert `PasswordHash` is non-null and `!= P`.
- **Traces to:** AC-5 ("passwords are stored hashed, never in plain text").

---

## Group G — No anonymous child / role escalation (auth-authz, product overrides)

### BE-TC-27 — Extra `roles` JSON field is ignored; user still gets only Parent
- **Type:** auth-authz / negative · **Priority:** P0
- **Steps:** send a raw JSON body that adds an undeclared field, e.g. `{ "email": <unique>, "password": "Str0ng@Pass", "acceptedTerms": true, "roles": ["Student","SuperAdmin","Admin"] }`.
- **Expected:** HTTP `200`, `Successed=true` (extra field ignored by model binding); the created user has only the `Parent` role (cross-check via BE-TC-05 technique); `Data` exposes **no** `roles` field.
- **Traces to:** AC-2 ("child not self-registered"); product override (server-decided role; no teacher role).

### BE-TC-28 — No anonymous `Register-Student` endpoint
- **Type:** negative · **Priority:** P0
- **Steps:** `POST /api/Users/Authentication/Register-Student` with `{}`.
- **Expected:** `404` (route absent) or `405` (method not allowed). No anonymous student-creation path exists.
- **Traces to:** AC-2; story ("a child account is not self-registered").

### BE-TC-29 — Anonymous user-creation (AddUser) is rejected with 401
- **Type:** auth-authz / negative · **Priority:** P0
- **Steps:** `POST /api/Users/UserManagement/AddUser` (no JWT) with a body that requests `Roles: ["Student"]`.
- **Expected:** `401 Unauthorized` (controller `[Authorize]` / AdminOnly policy) — no anonymous path can mint a Student or any other account.
- **Traces to:** AC-2 (no anonymous child creation); product decision.

---

## Group H — Captcha gate (conditional, config-dependent)

### BE-TC-30 — Default config (`Captcha:Enabled=false`): missing/blank `CaptchaToken` does not block registration
- **Type:** functional / config · **Priority:** P1
- **Preconditions:** test host runs with `Captcha:Enabled=false` (the default — confirm in the test app settings).
- **Steps:** register a valid parent with no `CaptchaToken`.
- **Expected:** HTTP `200`, `Successed=true` (verifier is a transparent no-op).
- **Traces to:** P1-13 BE-4 anti-automation gate (must not regress the happy path in default config).

### BE-TC-31 — Captcha enabled + invalid token → 400 (blocked before user creation)
- **Type:** anti-automation / negative · **Priority:** P2 · **Status: CONDITIONAL**
- **Preconditions:** requires a host configured with `Captcha:Enabled=true` and a stub/failing verifier. **Mark BLOCKED if the integration harness cannot toggle captcha config** — the default Testing profile disables it.
- **Steps:** with captcha enabled, register with an invalid/blank `CaptchaToken`.
- **Expected:** `400`, `Successed=false`, localized captcha-failure message; **no** account created (the gate runs before `FindByEmailAsync`/`CreateAsync`).
- **Traces to:** P1-13 BE-4.
- **Blocker (if applicable):** captcha config not toggleable in the integration harness → record as not-run with reason; the enabled-path is covered separately by `P1_13_BE4_Captcha_Tests.cs`.

---

## Group I — Envelope / contract shape

### BE-TC-32 — Success envelope carries all `BaseResponse` keys with correct spelling
- **Type:** functional / contract · **Priority:** P1
- **Steps:** register a valid parent; inspect root JSON.
- **Expected:** root contains `successed` (spelled **Successed** in C#, serialised camelCase `successed`), `statusCode`, `message`, `data`, `errors`. `successed=true`, `errors` empty, `data` is the `JwtAuthResponse` object.
- **Traces to:** AC-1, AC-6; CONVENTIONS (`Successed` spelling, envelope shape).

### BE-TC-33 — 422 envelope carries `statusCode`, `successed`, `message`, `errors`
- **Type:** contract · **Priority:** P1
- **Steps:** send `{ Email: "not-valid", Password: "", AcceptedTerms: true }`.
- **Expected:** HTTP `422`; root has all four envelope keys; `successed=false`; `message` present (`"Validation Failed"`); `errors[]` non-empty with `{ propertyName, errorMessage }` items.
- **Traces to:** AC-6.

### BE-TC-34 — Multiple simultaneous validation failures aggregate into `Errors[]`
- **Type:** validation / boundary · **Priority:** P2
- **Steps:** send `{ Email: "bad", Password: "x", AcceptedTerms: false }`.
- **Expected:** 422; `Errors[]` contains items for **each** failing rule (email, password, consent) — failures aggregate, not first-wins.
- **Traces to:** AC-6.

---

## Group J — Negative / robustness

### BE-TC-35 — Empty JSON body `{}` → 422
- **Type:** negative · **Priority:** P1
- **Steps:** `POST` body `{}`.
- **Expected:** 422 with `Errors[]` covering required fields (email, password, consent). No 500.
- **Traces to:** AC-6.

### BE-TC-36 — Malformed JSON → 400 (not 500)
- **Type:** negative · **Priority:** P2
- **Steps:** `POST` body `"{ this is not json"` with `Content-Type: application/json`.
- **Expected:** `400` (model-binding/parse failure); not a `500`. Graceful rejection.
- **Traces to:** robustness (no AC; defensive).

### BE-TC-37 — Oversized email local-part / very long input does not 500
- **Type:** boundary · **Priority:** P2
- **Steps:** register with a syntactically-valid but very long email (e.g. 300-char local-part) + valid password.
- **Expected:** deterministic response (422 invalid, or 200 accepted if within Identity limits) — **never** an unhandled 500. Record actual.
- **Traces to:** robustness / AC-6 edge.

### BE-TC-38 — GET on the register route is not allowed
- **Type:** negative · **Priority:** P2
- **Steps:** `GET /api/Users/Authentication/Register-Parent`.
- **Expected:** `405 Method Not Allowed` (endpoint is POST-only).
- **Traces to:** contract hardening.

---

## Group K — Regression (existing auth unaffected by P1-01 seed changes)

### BE-TC-39 — Seeded admin/basic accounts can still sign in
- **Type:** regression · **Priority:** P1 · **Status: ENVIRONMENT-DEPENDENT**
- **Preconditions:** seeded `superadmin` / `basicuser` exist in the test environment. (The dev seed creates them; the Testing profile may not — confirm.)
- **Steps:** `POST Sign-In` with the seeded credentials (`superadmin` / `123Pa$$word!`, `basicuser` / `123Pa$$word!`).
- **Expected:** `200`, `Successed=true` — the idempotent `RoleSeeder` change (PascalCase + existence checks) did not break existing seeded sign-ins.
- **Traces to:** plan blocker (RoleSeeder casing change must not regress existing seeds).
- **Note:** if the Testing profile does not seed these accounts, this is covered by the register→sign-in round-trip (BE-TC-04); mark this case N/A with reason.

---

## Priority roll-up (39 cases)
| Priority | Count | IDs |
|---|---|---|
| **P0** | 18 | 01, 04, 05, 09, 10, 11, 12, 13, 14, 16, 17, 18, 22, 25, 26, 27, 28, 29 |
| **P1** | 12 | 02, 03, 06, 15, 19, 20, 23, 30, 32, 33, 35, 39 |
| **P2** | 9 | 07, 08, 21, 24, 31, 34, 36, 37, 38 |

> Authoritative priority is the **Priority** field on each case. Implement all P0 and P1 cases; P2 are stretch but recommended. Cases marked **CONDITIONAL/ENVIRONMENT-DEPENDENT** (BE-TC-31; BE-TC-39 if seeds absent) must be recorded as not-run with the stated reason rather than dropped.
