# Backend Test Cases — P1-10 (Admin sign-in & dashboard shell)

**Target agent:** `api-tester` — implement each case 1:1 as an integration test against the running API.
**Fixture:** reuse `LearnexiaWebAppFactory` + `ApplyMigrationsAndSeedAsync()` (Testcontainers Postgres, `UseEnvironment("Testing")`). Recommended new file: `backend/tests/Learnexia.IntegrationTests/P1_10_AdminSignIn_Tests.cs`.
**Envelope reminder:** the controller path serialises camelCase (Newtonsoft); the `ValidationException`→422 path serialises PascalCase (System.Text.Json). Use a case-insensitive property lookup (the `TryProp` helper in `P1_05_RBAC_Tests`). The success flag key is **`successed`** / `Successed` — do not "fix" the spelling.

## Seeded test identities

| Handle | UserName | Password | Roles | How obtained |
|---|---|---|---|---|
| Admin | `superadmin` | `123Pa$$word!` | Basic, **Admin**, **SuperAdmin** | Sign-In |
| Basic | `basicuser` | `123Pa$$word!` | Basic (no Admin/Parent/Student) | Sign-In |
| Parent | (unique email) | `Str0ng@Pass` | Parent | `POST /api/Users/Authentication/Register-Parent` |

## Endpoints under test

| Purpose | Method + route | Auth |
|---|---|---|
| Sign-In | `POST /api/Users/Authentication/Sign-In` | `[AllowAnonymous]` |
| Refresh | `POST /api/Users/Authentication/Refresh-Token` | `[AllowAnonymous]` |
| Sign-Out | `POST /api/Users/Authentication/Sign-Out` | `[Authorize]` |
| Register-Parent (only anon account-mint) | `POST /api/Users/Authentication/Register-Parent` | `[AllowAnonymous]` |
| Me (role source for the FE gate) | `GET /api/Users/Me` | `[Authorize]` (not role-gated) |
| Admin role/claim CRUD (AdminOnly) | `GET /api/Users/Authorzation/RoleList`, `POST /api/Users/Authorzation/Create` | `[Authorize(AdminOnly)]` |
| Admin user mgmt (AdminOnly) | `POST /api/Users/UserManagement/AddUser`, `GET /api/Users/UserManagement/GetUserProfile` | `[Authorize(AdminOnly)]` |

> `AdminOnly` = `RequireRole("Admin","SuperAdmin")`. Anonymous → **401**; authenticated non-admin → **403**. The route class name `Authorzation` is a deliberate (existing) spelling — `[controller]` resolves to `Authorzation`, not `Authorization`.

---

## Group A — Admin sign-in issues a JWT · AC-1 / AC-7

### BE-TC-01 — Admin signs in with valid credentials → 200 + JWT
- **Type:** functional · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:**
  1. `POST /api/Users/Authentication/Sign-In` body `{ "userName": "superadmin", "password": "123Pa$$word!" }`.
- **Expected result:** HTTP **200**; body is `BaseResponse<JwtAuthResponse>` with `successed = true`; `data.accessToken` is a non-empty string; `data.userId` > 0.
- **Traces to:** AC-1, BE-2.

### BE-TC-02 — Sign-in response envelope shape; no roles in the payload
- **Type:** functional · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:** 1. Sign in as `superadmin`.
- **Expected result:** `data` contains `accessToken` (non-empty), `refreshToken.tokenString` (non-empty), `userId` (int), `isFirstLogin` (bool), `sessionTimeout`, `sessionId`. `data` contains **no** `roles`/`role` field — assert its absence so the FE contract (roles come from `Me`, not the sign-in payload) stays honest.
- **Traces to:** AC-1, BE-4 (contract detail).

### BE-TC-03 — Issued JWT carries Admin + SuperAdmin role claims
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:**
  1. Sign in as `superadmin`; capture `accessToken`.
  2. Decode the JWT payload (`JwtSecurityTokenHandler.ReadJwtToken`).
- **Expected result:** the token's role claims include both **`Admin`** and **`SuperAdmin`** (PascalCase, verbatim role Names). At least one role claim must equal `Admin` so the `AdminOnly` ordinal role match succeeds.
- **Traces to:** AC-7, BE-2.

### BE-TC-04 — Admin token is accepted by an AdminOnly endpoint (claim → policy round-trip)
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:**
  1. Sign in as `superadmin`; capture token.
  2. `GET /api/Users/Authorzation/RoleList` with `Authorization: Bearer {token}`.
- **Expected result:** HTTP **200**; `successed = true`. Proves the seeded admin's role claim actually satisfies `AdminOnly` end-to-end (the casing/round-trip guard).
- **Traces to:** AC-7, AC-3 (positive side), BE-2/BE-3.

---

## Group B — Sign-in negative & boundary paths (security-relevant)

### BE-TC-05 — Wrong password → 400 generic invalid-credentials
- **Type:** negative · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:** 1. Sign in `{ "userName": "superadmin", "password": "WrongPass!1" }`.
- **Expected result:** HTTP **400** (not 401, not 422); `successed = false`; the generic localized "invalid credentials" message (does not reveal that the user exists).
- **Traces to:** AC-1 negative; `SignInCommandHandler` anti-enumeration.

### BE-TC-06 — Unknown user → 400, identical to wrong password (anti-enumeration parity)
- **Type:** negative · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** none.
- **Steps:**
  1. Sign in `{ "userName": "definitely_not_a_user_<guid>", "password": "WrongPass!1" }`.
  2. Compare status + message to BE-TC-05.
- **Expected result:** HTTP **400**, `successed = false`, **same generic message and status** as the wrong-password case — a non-existent user is indistinguishable from a wrong password.
- **Traces to:** AC-1 negative; anti-enumeration guarantee.

### BE-TC-07 — Missing UserName/Password → 422 validation envelope
- **Type:** validation · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** none.
- **Steps:** 1. `POST .../Sign-In` body `{ }` (or `{ "userName": "" }`).
- **Expected result:** HTTP **422**; PascalCase error envelope with `Errors[]` populated; `Successed = false`. (`SignInCommand` is an `ICommand` → `ValidationBehavior` runs.)
- **Traces to:** validation contract (CONVENTIONS §4); AC-1 negative.

### BE-TC-08 — Account lockout after repeated failures → 400 too-many-attempts
- **Type:** boundary · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** a **dedicated disposable user** (do NOT lock out `superadmin`/`basicuser`, which other cases reuse — create one via admin `AddUser` and set a known password, or register a throwaway parent).
- **Steps:**
  1. Sign in with the correct userName + wrong password **5 times** (`MaxFailedAccessAttempts = 5`, 5-min window).
  2. Sign in a 6th time even with the **correct** password.
- **Expected result:** at the lockout threshold the response is HTTP **400** with the localized "too many failed attempts" message (distinct from the generic invalid-credentials message); the correct-password attempt while locked still returns the lockout 400.
- **Traces to:** AC-1 negative; lockout (`lockoutOnFailure: true`).

### BE-TC-09 — Deactivated account → 400 account-deactivated
- **Type:** negative · **Priority:** P2 · **Target:** `api-tester`
- **Preconditions / seed:** a user whose `IsActive == false`.
- **Steps:** 1. Sign in with that user's otherwise-valid credentials.
- **Expected result:** HTTP **400**; `successed = false`; localized "account deactivated" message (distinct from invalid-credentials).
- **Traces to:** AC-1 negative.
- **Blocker note:** if no HTTP-reachable path sets `IsActive = false` in this story's surface, mark **BLOCKED** with that reason; do not drop the case.

---

## Group C — Role source for the dashboard gate · AC-6

### BE-TC-10 — `GET /api/Users/Me` for admin returns the Admin role
- **Type:** functional · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:**
  1. Sign in as `superadmin`; capture token.
  2. `GET /api/Users/Me` with the bearer token.
- **Expected result:** HTTP **200**; `successed = true`; `data.roles` (PascalCase) contains **`Admin`** (and `SuperAdmin`). This is the role source the admin dashboard reads — NOT the sign-in payload.
- **Traces to:** AC-6, BE-4.

### BE-TC-11 — `Me` for a Parent returns Parent only (non-admin would be gate-rejected)
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** a freshly registered Parent.
- **Steps:**
  1. Register a parent; capture token.
  2. `GET /api/Users/Me` with the parent token.
- **Expected result:** HTTP **200**; `data.roles` contains **`Parent`** and does **not** contain `Admin`/`SuperAdmin`. Confirms the role source a client uses to deny dashboard entry to a non-admin.
- **Traces to:** AC-6, AC-3.

### BE-TC-12 — `Me` requires auth (anonymous → 401)
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** none.
- **Steps:** 1. `GET /api/Users/Me` with no Authorization header.
- **Expected result:** HTTP **401** (real HTTP 401, not a 200 fake-envelope).
- **Traces to:** AC-6 (auth boundary).

---

## Group D — Admin-only authorization matrix · AC-3 / AC-8 (the weighted core)

> Validated on the real `AdminOnly` controllers (`Authorzation`, `UserManagement`). The same matrix protects every future admin endpoint.

### BE-TC-13 — Anonymous on an AdminOnly GET → 401 (real HTTP 401)
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Steps:** 1. `GET /api/Users/Authorzation/RoleList` with no token.
- **Expected result:** HTTP **401** — a real bearer challenge, not 200 with a `successed=false` body, and not 500.
- **Traces to:** AC-3, Brief AC#8.

### BE-TC-14 — Anonymous on an AdminOnly POST → 401
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Steps:** 1. `POST /api/Users/Authorzation/Create` body `{ "roleName": "TestRole" }` with no token.
- **Expected result:** HTTP **401**.
- **Traces to:** AC-3, Brief AC#8.

### BE-TC-15 — Parent token on an AdminOnly GET → 403
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** registered Parent.
- **Steps:** 1. `GET /api/Users/Authorzation/RoleList` with the Parent bearer token.
- **Expected result:** HTTP **403** (authenticated but not permitted; real HTTP 403, not a fake-200).
- **Traces to:** AC-3.

### BE-TC-16 — Parent token on an AdminOnly POST → 403
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Steps:** 1. `POST /api/Users/Authorzation/Create` `{ "roleName": "TestRole" }` with a Parent token.
- **Expected result:** HTTP **403**.
- **Traces to:** AC-3.

### BE-TC-17 — Basic-role token on an AdminOnly endpoint → 403
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `basicuser` (Basic role only).
- **Steps:**
  1. Sign in as `basicuser`; capture token.
  2. `GET /api/Users/Authorzation/RoleList` with that token.
- **Expected result:** HTTP **403**. An authenticated, non-admin, non-parent role is still denied — proves the gate is role-affirmative (requires Admin/SuperAdmin), not merely "deny Parent".
- **Traces to:** AC-3.

### BE-TC-18 — Admin/SuperAdmin token on an AdminOnly GET → 200
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Steps:** 1. Sign in `superadmin`. 2. `GET /api/Users/Authorzation/RoleList` with the token.
- **Expected result:** HTTP **200**; `successed = true`. Positive side of the matrix.
- **Traces to:** AC-3 (positive), AC-7.

### BE-TC-19 — Admin token on an AdminOnly POST → not 401/403 (authz passes)
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Steps:** 1. Sign in `superadmin`. 2. `POST /api/Users/Authorzation/Create` with a unique `roleName`.
- **Expected result:** status is **not 401 and not 403** (200/422/400 business outcomes all acceptable — the point is that authz passes for an admin).
- **Traces to:** AC-3 (positive).

### BE-TC-20 — Tampered/malformed bearer token on an AdminOnly endpoint → 401 (not 500)
- **Type:** negative · **Priority:** P0 · **Target:** `api-tester`
- **Steps:** 1. `GET /api/Users/Authorzation/RoleList` with `Authorization: Bearer malformed.jwt.token`.
- **Expected result:** HTTP **401** (the bearer middleware rejects invalid tokens; must not 500, must not 200).
- **Traces to:** AC-3 (robustness).

### BE-TC-21 — Expired admin token on an AdminOnly endpoint → 401
- **Type:** boundary · **Priority:** P2 · **Target:** `api-tester`
- **Preconditions / seed:** an expired-but-well-formed JWT for an admin.
- **Steps:** 1. Present the expired JWT to `GET /api/Users/Authorzation/RoleList`.
- **Expected result:** HTTP **401**.
- **Traces to:** AC-3, AC-4 (token lifetime).
- **Blocker note:** if no seam exists to mint/expire a token within the suite, mark **BLOCKED** with that reason; do not drop.

### BE-TC-22 — `GetUserProfile` is itself AdminOnly (anonymous → 401, Parent → 403, Admin → 200)
- **Type:** auth-authz · **Priority:** P1 · **Target:** `api-tester`
- **Preconditions / seed:** registered Parent + seeded `superadmin`.
- **Steps:**
  1. `GET /api/Users/UserManagement/GetUserProfile` with no token → expect **401**.
  2. Same with a Parent token → expect **403**.
  3. Same with the `superadmin` token → expect **200** (`successed = true`, `data.roles` present).
- **Expected result:** as above. Confirms the brief's contract detail: `GetUserProfile` is **admin-gated** (a non-admin cannot use it to learn its own role; that is what `Me` is for).
- **Traces to:** AC-3, AC-6 (correct role-source separation).

---

## Group E — No public admin self-registration · AC-2 / product override

### BE-TC-23 — The only anonymous account-mint is Register-Parent (yields Parent, never Admin)
- **Type:** negative · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** none.
- **Steps:**
  1. `POST /api/Users/Authentication/Register-Parent` with a valid unique body; capture token.
  2. `GET /api/Users/Me` with that token.
- **Expected result:** registration succeeds (**200**) but `Me.roles` is **`Parent`** only — never `Admin`/`SuperAdmin`/`Student`. There is no anonymous request shape that mints an admin.
- **Traces to:** AC-2, CLAUDE.md no-self-register override.

### BE-TC-24 — Admin-creation surface (`AddUser`) is gated; not anonymously reachable
- **Type:** auth-authz · **Priority:** P0 · **Target:** `api-tester`
- **Steps:**
  1. `POST /api/Users/UserManagement/AddUser` body `{ email, userName, fullName, roles: ["Admin"] }` with **no token**.
  2. Same with a Parent token.
- **Expected result:** step 1 → **401**; step 2 → **403**. No unauthenticated or non-admin caller can mint an Admin (or any) account.
- **Traces to:** AC-2, AC-3.

### BE-TC-25 — Admin can provision a user via the gated surface (seed/invite path works)
- **Type:** functional · **Priority:** P2 · **Target:** `api-tester`
- **Steps:** 1. Sign in `superadmin`. 2. `POST /api/Users/UserManagement/AddUser` with a unique user body.
- **Expected result:** **200/201**; `successed = true`. Confirms that non-parent accounts are provisioned only through the admin-bound surface (invite/seed only).
- **Traces to:** AC-2 (provisioning is admin/invite-bound).

---

## Group F — Configured admin seed (environment-gated) · BE-1

### BE-TC-26 — `SeedConfiguredAdminAsync` is a no-op when `AdminSeed:*` is unset (no committed credential)
- **Type:** persistence/seed · **Priority:** P2 · **Target:** `api-tester`
- **Preconditions / seed:** default `Testing` host (no `AdminSeed:Email`/`AdminSeed:Password`).
- **Steps:**
  1. Run the normal seed (`ApplyMigrationsAndSeedAsync`).
  2. Assert no account exists for any configured-admin email beyond the legacy `superadmin`/`basicuser`.
- **Expected result:** `SeedConfiguredAdminAsync` created **no** account because no config was supplied — confirming there is no committed admin credential in the default path.
- **Traces to:** BE-1, AC-2 (no committed credential).

### BE-TC-27 — Configured-admin seed provisions an Admin-only account, idempotently
- **Type:** persistence/seed · **Priority:** P2 · **Target:** `api-tester` · **ENVIRONMENT-GATED (may be BLOCKED)**
- **Preconditions / seed:** a bespoke test host that supplies `AdminSeed:Email = qa-admin@test` + `AdminSeed:Password = <strong>` via in-memory config. The default `LearnexiaWebAppFactory` does **not** set these.
- **Steps:**
  1. Boot with `AdminSeed:*` configured; run the seed **twice**.
  2. Sign in with the configured email + password.
  3. `GET /api/Users/Me`.
- **Expected result:** the configured admin exists exactly once (second seed creates no duplicate — idempotent via `FindByEmailAsync`); sign-in succeeds (**200**); `Me.roles` contains **`Admin`** and **not** `SuperAdmin` (this seed grants Admin only).
- **Traces to:** BE-1.
- **Blocker note:** if `api-tester` does not stand up a config-overriding factory, mark **BLOCKED** with reason "requires an `AdminSeed:*` config host"; document the recipe above; do not drop.

---

## Group G — Admin session: refresh & sign-out · AC-4 + regression baseline

### BE-TC-28 — Admin refresh + sign-out round-trip; existing suites stay green
- **Type:** functional / regression · **Priority:** P0 · **Target:** `api-tester`
- **Preconditions / seed:** seeded `superadmin`.
- **Steps:**
  1. Sign in `superadmin`; capture `accessToken` + `refreshToken.tokenString`.
  2. `POST /api/Users/Authentication/Refresh-Token` `{ accessToken, refreshToken }` → expect **200**, a new non-empty `accessToken`, and a **rotated** `refreshToken.tokenString` (differs from the one sent).
  3. `POST /api/Users/Authentication/Sign-Out` with the current bearer token → expect **200**, `successed = true`.
  4. Re-attempt `Refresh-Token` with the now-revoked refresh token → expect **401** (`successed = false`, not 500).
  5. Confirm the existing `P1_02_RefreshAndSignOut_Tests` and `P1_05_RBAC_Tests` suites still pass (regression baseline — P1-10 changes no endpoint behaviour).
- **Expected result:** admin refresh + sign-out behave identically to the P1-02 flow; the revoked refresh token is rejected with 401; existing suites stay green.
- **Traces to:** AC-4; regression guard.

---

## ID index (stable, final — 28 cases)

| ID | Title | Type | Prio | Notes |
|---|---|---|---|---|
| BE-TC-01 | Admin valid sign-in → 200 + JWT | functional | P0 | |
| BE-TC-02 | Sign-in envelope shape; no roles in payload | functional | P1 | |
| BE-TC-03 | JWT carries Admin+SuperAdmin claims | auth-authz | P0 | |
| BE-TC-04 | Admin token accepted by AdminOnly (round-trip) | auth-authz | P0 | |
| BE-TC-05 | Wrong password → 400 generic | negative | P0 | |
| BE-TC-06 | Unknown user → 400 (anti-enumeration parity) | negative | P1 | |
| BE-TC-07 | Missing fields → 422 | validation | P1 | |
| BE-TC-08 | Lockout after 5 failures → 400 | boundary | P1 | dedicated user |
| BE-TC-09 | Deactivated account → 400 | negative | P2 | may be BLOCKED |
| BE-TC-10 | `Me` for admin → Admin role | functional | P0 | |
| BE-TC-11 | `Me` for Parent → Parent only | auth-authz | P1 | |
| BE-TC-12 | `Me` anonymous → 401 | auth-authz | P1 | |
| BE-TC-13 | Anonymous AdminOnly GET → 401 | auth-authz | P0 | |
| BE-TC-14 | Anonymous AdminOnly POST → 401 | auth-authz | P1 | |
| BE-TC-15 | Parent AdminOnly GET → 403 | auth-authz | P0 | |
| BE-TC-16 | Parent AdminOnly POST → 403 | auth-authz | P1 | |
| BE-TC-17 | Basic-role AdminOnly → 403 | auth-authz | P0 | |
| BE-TC-18 | Admin AdminOnly GET → 200 | auth-authz | P0 | |
| BE-TC-19 | Admin AdminOnly POST → not 401/403 | auth-authz | P1 | |
| BE-TC-20 | Tampered token → 401 (not 500) | negative | P0 | |
| BE-TC-21 | Expired token → 401 | boundary | P2 | may be BLOCKED |
| BE-TC-22 | `GetUserProfile` is AdminOnly (401/403/200) | auth-authz | P1 | |
| BE-TC-23 | Register-Parent yields Parent, never Admin | negative | P0 | |
| BE-TC-24 | `AddUser` gated (anon→401, Parent→403) | auth-authz | P0 | |
| BE-TC-25 | Admin can provision a user via gated surface | functional | P2 | |
| BE-TC-26 | Configured-admin seed no-op when unset | persistence/seed | P2 | |
| BE-TC-27 | Configured-admin seed idempotent, Admin-only | persistence/seed | P2 | ENV-GATED / may be BLOCKED |
| BE-TC-28 | Admin refresh + sign-out + regression baseline | functional/regression | P0 | |

**Totals:** 28 cases — P0 ×12, P1 ×10, P2 ×6. By type: auth-authz ×12, functional ×4, negative ×4, boundary ×3, persistence/seed ×3, validation ×1, regression (folded into BE-TC-28).
