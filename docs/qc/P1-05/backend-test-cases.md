# Backend Test Cases — P1-05 Role-based access control

> Target agent: **`api-tester`** · Surface: HTTP API (integration tests).
> Harness: `LearnexiaWebAppFactory` (Testcontainers PostgreSQL, env "Testing", placeholder JWT secret tolerated).
> Envelope: handlers return `BaseResponse<T>` with success flag spelled **`Successed`**. Auth/authz denials
> are **real HTTP 401/403**, NOT a `200` with `Successed=false`.
> An existing file `backend/tests/Learnexia.IntegrationTests/P1_05_RBAC_Tests.cs` already covers many of these —
> extend/validate it rather than duplicating.

## Conventions used below
- **Admin token** = sign in `superadmin / 123Pa$$word!` via `POST api/Users/Authentication/Sign-In` → `data.accessToken` (roles: Basic, Admin, SuperAdmin).
- **Basic token** = sign in `basicuser / 123Pa$$word!` (role: Basic only — neither Admin, Parent, nor Student).
- **Parent token** = `POST api/Users/Authentication/Register-Parent` `{ Email, Password, AcceptedTerms:true }` → `data.accessToken` (role: Parent).
- **Student token** = no seeded Student user; create via admin `POST api/Users/UserManagement/AddUser` `{ Email, UserName, FullName, Roles:["Student"] }` then seed/obtain a known password (see Q-note in README; tester may need to seed a fixed-password Student in the factory).
- Route note: real route is **`api/Users/Authorzation/...`** (source class is `AuthorzationController`, a typo).

---

## Group A — Unauthenticated → 401 (brief AC-2)

### BE-TC-01 — Admin-only role list, no token → 401
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with **no** `Authorization` header.
- **Expected:** HTTP **401** Unauthorized (JWT bearer challenge fires). Not 200, not 403, not 500.
- **Traces to:** AC-2.
- **Note (AC-8 pairing):** confirm this is a true 401 challenge from the gate, not a business envelope.

### BE-TC-02 — Admin-only role CRUD create, no token → 401
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `POST api/Users/Authorzation/Create` body `{ "RoleName": "TestRole" }` with no token.
- **Expected:** HTTP **401**. The role/claim CRUD surface is fully gated (`AdminOnly` class-level).
- **Traces to:** AC-2, AC-3.

### BE-TC-03 — User-management endpoint, no token → 401
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `POST api/Users/UserManagement/AddUser` body `{ Email, UserName, FullName, Roles:["Student"] }` with no token.
- **Expected:** HTTP **401** (`UserManagementController` is class-level `AdminOnly`).
- **Traces to:** AC-2, AC-3.

### BE-TC-04 — Parent endpoint, no token → 401
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET api/Parent/My-Children` with no token.
- **Expected:** HTTP **401** (`ParentController` is class-level `[Authorize(Roles=...)]`).
- **Traces to:** AC-2.

---

## Group B — Wrong role → 403 (brief AC-1, AC-3, AC-5)

### BE-TC-05 — Role list with Parent token → 403
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** valid Parent token.
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with Parent bearer.
- **Expected:** HTTP **403** Forbidden (authenticated but lacks Admin/SuperAdmin).
- **Traces to:** AC-1, AC-3, AC-5.

### BE-TC-06 — Role list with Basic token → 403
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** valid Basic token (`basicuser`).
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with Basic bearer.
- **Expected:** HTTP **403** (Basic is neither Admin nor SuperAdmin).
- **Traces to:** AC-1, AC-3.

### BE-TC-07 — Role list with Admin/SuperAdmin token → 200 + envelope
- **Type:** auth-authz / functional · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** valid Admin token (`superadmin`).
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with Admin bearer.
- **Expected:** HTTP **200**; body has `Successed == true` (envelope). Confirms `AdminOnly` policy does
  not lock out admins (PascalCase role match works).
- **Traces to:** AC-1 (positive path), AC-3.

### BE-TC-08 — Role create with Parent token → 403
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** valid Parent token.
- **Steps:** 1) `POST api/Users/Authorzation/Create` body `{ "RoleName": "TestRole" }` with Parent bearer.
- **Expected:** HTTP **403** — gate fires before any business logic.
- **Traces to:** AC-1, AC-3.

### BE-TC-09 — User-management AddUser with non-admin token → 403
- **Type:** auth-authz / negative · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** valid Parent token (or Basic token).
- **Steps:** 1) `POST api/Users/UserManagement/AddUser` valid body with Parent/Basic bearer.
- **Expected:** HTTP **403** — only Admin/SuperAdmin may provision users.
- **Traces to:** AC-1, AC-3.

### BE-TC-10 — User-management AddUser with Admin token → not 401/403
- **Type:** auth-authz / functional · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** valid Admin token.
- **Steps:** 1) `POST api/Users/UserManagement/AddUser` body `{ Email:<unique>, UserName:<unique>, FullName:"QC", Roles:["Student"] }` with Admin bearer.
- **Expected:** HTTP **200/201** (or 422 on a validation issue) — **must not** be 401 or 403. Confirms admin passes the gate.
- **Traces to:** AC-3.

---

## Group C — Anonymous surface stays open (brief AC-8) + invalid token

### BE-TC-11 — Sign-In remains anonymous → not 401
- **Type:** regression / auth-authz · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `POST api/Users/Authentication/Sign-In` body `{ UserName:"superadmin", Password:"123Pa$$word!" }` with no token.
- **Expected:** HTTP **200** (and not 401). Confirms no stray gate was added to authn.
- **Traces to:** AC-8.

### BE-TC-12 — Register-Parent / Validate-Token / Refresh-Token remain anonymous → not 401
- **Type:** regression / auth-authz · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:**
  1) `POST api/Users/Authentication/Register-Parent` valid body, no token → expect 200 (not 401).
  2) `POST api/Users/Authentication/Validate-Token` `{ AccessToken:"garbage.token.value" }`, no token → expect status **≠ 401** (business failure 400/424, not a gate challenge).
  3) `POST api/Users/Authentication/Refresh-Token` `{ AccessToken:"garbage", RefreshToken:"garbage" }`, no token → expect status **≠ 401**.
- **Expected:** none of the three returns a 401 auth challenge; they execute their handlers anonymously.
- **Traces to:** AC-8.

### BE-TC-13 — Health probes remain anonymous → 200
- **Type:** regression · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET /health` no token. 2) `GET /health/live` no token.
- **Expected:** both HTTP **200** (probes never gated).
- **Traces to:** AC-8.

### BE-TC-14 — Invalid/tampered bearer token → 401 (not 500)
- **Type:** negative / boundary · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with `Authorization: Bearer malformed.jwt.token`.
- **Expected:** HTTP **401** — a malformed JWT is rejected by the bearer middleware, never reaches the handler, never 500s.
- **Traces to:** AC-2 (invalid-token boundary).

---

## Group D — Family/self scope isolation (brief AC-4, AC-5; current mechanism)

> The dedicated `FamilyScopeAuthorizationHandler` was removed (P2-12). Isolation is now enforced
> inside Parent-module handlers via a link-row check. These cases assert the **observable HTTP
> behavior**, which is what matters for the AC.

### BE-TC-15 — Parent B cannot claim a child already linked to Parent A (cross-family deny)
- **Type:** auth-authz / negative (IDOR) · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Admin token; one Student account created via `AddUser` (email known); Parent A token; Parent B token.
- **Steps:**
  1) Admin `POST api/Users/UserManagement/AddUser` → create Student (email `S`).
  2) Parent A `POST api/Parent/Link-Child` `{ ChildEmail: S }` → expect 200.
  3) Parent B `POST api/Parent/Link-Child` `{ ChildEmail: S }`.
- **Expected:** step 3 returns a **non-2xx** result and `Successed == false` — Parent B cannot steal a child linked to Parent A.
- **Traces to:** AC-4, AC-5 ("other students' data stays protected").

### BE-TC-16 — Parent B does not see Parent A's children in My-Children
- **Type:** auth-authz / persistence (isolation) · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** Admin token; a Student created + linked to Parent A; fresh Parent B token (no children).
- **Steps:** 1) After Parent A links a child, Parent B `GET api/Parent/My-Children`.
- **Expected:** HTTP **200**, `data` array **length 0** — actor resolved from JWT; no cross-family leakage.
- **Traces to:** AC-4.

### BE-TC-17 — Parent actor is taken from JWT, not request body (self-scope integrity)
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** Parent A token; a child linked to Parent A; Parent B token.
- **Steps:** 1) Parent B `GET api/Parent/My-Children` (Parent B's token) — even if a spoofed parent id is supplied anywhere, the handler resolves the actor from the JWT.
- **Expected:** Parent B sees only Parent B's own children (0 here), never Parent A's — confirms no body/param can override the JWT-derived identity.
- **Traces to:** AC-4 (IDOR via parameter tampering).

### BE-TC-18 — Admin/SuperAdmin allowed into ParentController (support path)
- **Type:** auth-authz / functional · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** Admin token.
- **Steps:** 1) `GET api/Parent/My-Children` with Admin bearer.
- **Expected:** HTTP **200**, `Successed == true` (Admin/SuperAdmin are in the role gate `Parent,Admin,SuperAdmin`; admin has no children → empty list is fine).
- **Traces to:** AC-4 (admin always-allowed branch).

---

## Group E — Parent is not a learner + claims scoped to real modules (brief AC-5, AC-6)

### BE-TC-19 — Parent token cannot start a quiz attempt (Student-only route) → 403
- **Type:** auth-authz / negative · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** valid Parent token; any existing lesson id (or use a placeholder id — the gate fires before lookup).
- **Steps:** 1) `POST api/learning/Quizzes/{lessonId}/Attempt` with Parent bearer.
- **Expected:** HTTP **403** — the route is `[Authorize(Roles = "Student")]`; a Parent is not a learner.
- **Traces to:** AC-5.
- **Note:** confirms the "parent cannot act as a learner" AC against the only Student-gated learner write surface.

### BE-TC-19b — Permission policies registered only for real modules (Learning, Parent)
- **Type:** functional / config · **Priority:** P2 · **Target:** api-tester
- **Preconditions:** none (white-box / config assertion; tester may assert via the running app's
  authorization options or by confirming no endpoint references a `Catalog.*` or other non-existent-module policy).
- **Steps:** 1) Inspect `Claims.GenerateModules()` result / registered policies; confirm modules are `Learning` and `Parent` only; confirm there is no dead policy for a removed module (e.g. `Catalog.*`).
- **Expected:** module list = `{ Learning, Parent }`; no `Catalog.*` policy exists; no `[Authorize(Policy="Catalog.*")]` attribute remains anywhere.
- **Traces to:** AC-6.

---

## Group F — Real-status discipline: 401/403 are real HTTP, not fake 200

### BE-TC-21 — 401 is a real HTTP 401, not a 200 envelope
- **Type:** auth-authz / contract · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` no token; capture the raw HTTP status line.
- **Expected:** status code is exactly **401**, not a 200 carrying `Successed=false`. Bearer challenge must fire at the HTTP layer.
- **Traces to:** AC-2 (contract integrity).

### BE-TC-22 — 401 envelope check on a second protected surface
- **Type:** auth-authz / contract · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** none.
- **Steps:** 1) `GET api/Parent/My-Children` no token.
- **Expected:** HTTP **401** (real), confirming the role-gated controller also challenges, not envelopes.
- **Traces to:** AC-2.

### BE-TC-23 — 403 is a real HTTP 403, not a 200 envelope
- **Type:** auth-authz / contract · **Priority:** P0 · **Target:** api-tester
- **Preconditions:** valid Parent token.
- **Steps:** 1) `GET api/Users/Authorzation/RoleList` with Parent bearer; capture raw HTTP status.
- **Expected:** status code is exactly **403** (ASP.NET Core authorization short-circuit), not 200/401.
- **Traces to:** AC-1.

---

## Group G — Secrets hardening (brief AC-7)

### BE-TC-24 — Committed config carries only the JWT secret placeholder
- **Type:** security / config · **Priority:** P1 · **Target:** api-tester
- **Preconditions:** repo checkout (config-inspection case, not an HTTP call).
- **Steps:**
  1) Read `backend/src/Host/Learnexia.Host/appsettings.json` `JwtSettings:Secret`.
  2) Assert it equals the documented `CHANGE_ME…` placeholder (not a strong/real secret).
  3) (Optional, if a host harness allows) construct a host in env `Production` with the placeholder/empty secret and assert `GuardJwtSecret` throws on startup.
- **Expected:** committed value is the placeholder; `GuardJwtSecret` fail-fasts on Production/Staging when the placeholder/empty is supplied (Development/Testing tolerated).
- **Traces to:** AC-7.
- **Note:** full "secret sourced from a secret store at runtime" is a deploy concern beyond integration tests — see README Q4.

---

## Group H — Flagged authz gap (NOT a P1-05 pass/fail; documents current behavior)

### BE-TC-20 — `GradesController` curriculum-authoring is anonymous (GAP)
- **Type:** auth-authz / negative (security finding) · **Priority:** P0 (finding) · **Target:** api-tester
- **Preconditions:** none.
- **Steps:**
  1) `GET api/learning/Grades/List` with no token.
  2) `POST api/learning/Grades/Create` body `{ ... }` with no token.
  3) `DELETE api/learning/Grades?id=1` with no token.
- **Expected (current, INSECURE behavior to record):** all reach the handler **without** a 401/403 —
  i.e. curriculum grades are world-readable AND world-writable/deletable.
- **DESIRED behavior (pending lead decision Q1):** reads = authenticated (401 without token);
  writes (Create/Update/Delete) = `AdminOnly` (403 for non-admin, 401 for no token).
- **Traces to:** Story AC ("admin-only curriculum endpoints reject non-admins") — currently **NOT met** for Grades.
- **Action:** record actual status codes; the diff between actual and desired is the defect for the
  execution report. Whether this blocks P1-05 is the lead's call (README Q1).
