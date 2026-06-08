# QC Test Plan & Coverage Report — P1-05 Role-based access control (BACKEND-ONLY)

> Story: `user-stories/Phase-1-Foundation/P1-05-role-based-access-control.md`
> Brief: `docs/briefs/P1-05.md` · Plan: `docs/plans/P1-05.md` · Task: `tasks/Backend/Phase-1-Foundation/P1-05-BE.md`
> Scope: **Backend API surface only.** No `frontend-test-cases.md` (no UI surface in this story).
> Author: QC test architect (design only — no test code, no execution).

---

## 1. Summary

P1-05 turns the dormant role/permission/authorization machinery **on** across the real module
controllers: it gates the previously-anonymous role/claim CRUD surface, confirms admin-only and
parent-role gates, verifies family/self-scope isolation, hardens the JWT secret out of committed
config, and confirms consistent **401 (unauthenticated) vs 403 (forbidden)** behavior. No teacher
role exists; the product roles are **Student, Parent, Admin** (+ legacy `SuperAdmin`).

**This pass was designed against the CURRENT state of `main`**, which has moved past the brief's
snapshot. The material deltas the test cases reflect (see §4 Open Questions for full detail):

- The `FamilyScopeAuthorizationHandler` referenced by AC-4/AC-5 and the plan **no longer exists** —
  it was removed in P2-12 (it read a `ParentStudent` link table that moved to the **Parent** module,
  and `FamilyScopeRequirement` was never wired into any policy/`AuthorizeAsync` call). Family/self
  scope is now enforced **per-handler inside the Parent module via a link-row check**. AC-4/AC-5
  cases therefore assert the **observable HTTP behavior** (cross-family `Link-Child` denial,
  `My-Children` isolation, admin/parent role gate), not a handler that is gone.
- The **Catalog module no longer exists** (it was demo scaffolding, replaced by `Learning`).
  `Claims.GenerateModules()` now returns `"Learning"` and `"Parent"` (not `"Catalog"`). AC-6 cases
  assert the current module list and that no dead policy for a non-existent module is registered.
- A **NEW authz gap** surfaced that did not exist in the brief snapshot: `GradesController`
  (`api/learning/Grades`) is **fully anonymous including Create/Update/Delete** — curriculum-authoring
  writes are world-writable. Flagged as a finding (BE-TC-20) and an open question.

### Counts

| Metric | Value |
|---|---|
| Total cases | 24 |
| Backend (`api-tester`) | 24 |
| Frontend | 0 (out of scope — backend-only run) |
| P0 | 14 |
| P1 | 8 |
| P2 | 2 |
| Cases blocked / not-yet-testable | 1 (BE-TC-20 — documents a real gap; see note) |

Surfaces under test (current `main`):

| Controller | Route | Current gate |
|---|---|---|
| `AuthorzationController` (Identity) | `api/Users/Authorzation/*` | `[Authorize(Policy = AdminOnly)]` (class) |
| `UserManagementController` (Identity) | `api/Users/UserManagement/*` | `[Authorize(Policy = AdminOnly)]` (class) |
| `ParentController` (Parent) | `api/Parent/*` | `[Authorize(Roles = "Parent,Admin,SuperAdmin")]` (class) |
| `AuthenticationController` (Identity) | `api/Users/Authentication/*` | `[AllowAnonymous]` on register/sign-in/validate/refresh/forgot/reset; `[Authorize]` on Sign-Out |
| `NotificationsController.List` (Notifications) | `api/.../Notifications/List` | `[Authorize(Policy = AdminOnly)]` (action) |
| `GradesController` (Learning) | `api/learning/Grades/*` | **NONE (anonymous)** — flagged gap |
| Health probes | `/health`, `/health/live` | Anonymous (by design) |

---

## 2. Coverage matrix (acceptance criterion → case IDs)

Acceptance criteria taken from the user story + brief AC-1..AC-8.

| Acceptance criterion | Case IDs | Covered? |
|---|---|---|
| **Story AC: wrong role → 403** (brief AC-1) | BE-TC-05, BE-TC-06, BE-TC-07, BE-TC-08, BE-TC-12, BE-TC-23 | Yes |
| **Story AC: unauthenticated → 401** (brief AC-2) | BE-TC-01, BE-TC-02, BE-TC-03, BE-TC-11, BE-TC-21, BE-TC-22 | Yes |
| **Story AC: students can't access parent reports / other students' data** (brief AC-4/AC-5) | BE-TC-15, BE-TC-16, BE-TC-17, BE-TC-18 | Yes (via HTTP family-scope behavior) |
| **Story AC: parents can't act as a learner** (brief AC-5) | BE-TC-14, BE-TC-19 | Partial — see §4 Q2 (no learner-write endpoint accepts a Parent today; asserted by absence of learner claims + Parent gate) |
| **Story AC: admin-only curriculum endpoints reject non-admins** (brief AC-3) | BE-TC-05..BE-TC-10, BE-TC-12, BE-TC-13 | Yes for role/claim CRUD + user mgmt; **GAP** for `GradesController` (BE-TC-20) |
| **Story AC: JWT secret out of committed appsettings** (brief AC-7) | BE-TC-24 | Partial — config-inspection case; full secret-store posture is a deploy concern (see §4 Q4) |
| **Brief AC-6: claims scoped to real modules only** | BE-TC-19 | Yes (asserts `Learning`/`Parent` modules; no dead policy) |
| **Brief AC-8: authn + health endpoints stay anonymous** | BE-TC-01-note, BE-TC-09, BE-TC-10, BE-TC-11 | Yes |

**Gap flagged:** `GradesController` curriculum-authoring writes are anonymous (BE-TC-20). This
contradicts the spirit of the story AC ("admin-only curriculum endpoints reject non-admins"). It is
**not** a P1-05 regression — the controller was authored later (P2-01) with authz deliberately
deferred — but it is a live access-control hole the QC pass must surface. Marked as a finding case,
not a pass/fail of P1-05's own deliverables. **Lead decision required (Q1).**

---

## 3. Risk notes (where cases are weighted)

1. **401 vs 403 confusion (highest weight).** The single most common RBAC defect is a gate that
   returns the wrong status: a real HTTP 401/403 vs a fake `200` with `Successed=false` envelope, or
   a policy whose backing claim/role nobody holds (locks out everyone, including admins). Heavy P0
   coverage on `AuthorzationController` for no-token→401, wrong-role→403, right-role→200, and explicit
   "real HTTP status, not a 200 envelope" assertions (BE-TC-21, BE-TC-22, BE-TC-23).
2. **Over/under-permissioning.** `AdminOnly` is registered with PascalCase role names
   (`Roles.Admin`/`Roles.SuperAdmin`); `RequireRole` compares ordinally. A casing drift would 403
   every admin. Cases assert admins actually pass (BE-TC-07, BE-TC-13) and non-admins (Parent, Basic)
   are denied (BE-TC-05, BE-TC-06, BE-TC-08, BE-TC-12).
3. **Family/self-scope IDOR.** Since the dedicated handler was removed, isolation now lives in Parent
   module handlers. Cases prove cross-family `Link-Child` theft is denied (BE-TC-15) and one parent
   cannot enumerate another's children (BE-TC-16), plus self-scoped endpoints resolve the actor from
   the JWT, not the body (BE-TC-17, BE-TC-18).
4. **Anonymous-surface regression.** The auth/health endpoints MUST stay anonymous; a stray
   class-level `[Authorize]` would lock out registration/sign-in entirely. Regression guards
   (BE-TC-09, BE-TC-10, BE-TC-11).
5. **Curriculum-authoring write hole (new).** `GradesController` POST/PUT/DELETE are anonymous —
   anyone can create/edit/delete curriculum grades (BE-TC-20). Weighted as a security finding.
6. **Legacy role noise.** The `Roles` enum still carries fund-management roles. No Learnexia policy
   should depend on them; not directly testable via HTTP, deferred to `security-auditor` (noted in §4).

---

## 4. Open questions / assumptions (lead must resolve before/with implementation)

- **Q1 (GAP — needs a decision). `GradesController` (`api/learning/Grades`) is fully anonymous,
  including Create/Update/Delete.** Anyone unauthenticated can author/delete curriculum grades. The
  story AC explicitly wants "admin-only curriculum endpoints reject non-admins." Options: (a) treat
  as an in-scope P1-05 fix and gate writes `AdminOnly` (reads authenticated); (b) log as a separate
  follow-up story and let BE-TC-20 record the current (insecure) behavior as a known gap.
  **QC recommendation: (a)** — it is exactly the access-control class this story is meant to close.
- **Q2 (AC-5 "parent is not a learner").** No learning-content **write/learner** endpoint accepts a
  Parent today (`QuizzesController` attempt endpoints are `[Authorize(Roles = "Student")]`, so a
  Parent already 403s there). AC-5 is therefore satisfiable only as: (i) Parent has **no** learner
  permission claim, and (ii) Parent is gated out of Student-only routes. BE-TC-14/BE-TC-19 assert
  this. Confirm this interpretation is acceptable rather than expecting a dedicated "parent blocked
  from lesson content" route.
- **Q3 (family-scope handler is gone).** The brief/plan AC-4 assumed a live
  `FamilyScopeAuthorizationHandler` + a `studentId` resource check. That handler was removed in
  P2-12; family-scope is now per-handler in the Parent module. The cases assert the **current**
  observable isolation (cross-family `Link-Child` denial, `My-Children` isolation). Confirm QC should
  validate the *current* mechanism rather than the removed handler.
- **Q4 (AC-7 secret).** `appsettings.json` ships the `CHANGE_ME…` placeholder (verified) and
  `GuardJwtSecret` fail-fasts on Production/Staging if empty/placeholder. BE-TC-24 inspects the
  committed config for a placeholder (not a real secret). The full "secret comes from a secret store"
  posture is a deployment/runtime concern an integration test cannot fully assert — confirm the
  committed-placeholder check + the existing `GuardJwtSecret` are the accepted evidence for AC-7.
- **Assumption (token minting).** Seeded users: `superadmin / 123Pa$$word!` (Basic+Admin+SuperAdmin)
  and `basicuser / 123Pa$$word!` (Basic). There is **no seeded Student-role user** — a Student
  account is created via `POST api/Users/UserManagement/AddUser` (admin) and a Parent via
  `POST api/Users/Authentication/Register-Parent`. Cases needing a Student token assume the tester
  mints one this way (or seeds one in the factory). Note: `AddUser` does not return a password, so a
  Student *token* may require seeding a known-password Student in the test factory — flagged in
  BE-TC-12/BE-TC-23 preconditions.
- **Note (route spelling).** The controller class is `AuthorzationController` (typo in source). The
  `[controller]` token yields the route segment **`Authorzation`** → `api/Users/Authorzation/...`.
  The plan's matrix shows a second typo `Autorzation`; the **correct** route is `Authorzation`. Cases
  use the correct spelling.

---

## 5. Handoff

| File | Goes to | Action |
|---|---|---|
| `docs/qc/P1-05/backend-test-cases.md` | `api-tester` | Implement BE-TC-01..BE-TC-24 as integration tests against the running API (reuse `LearnexiaWebAppFactory`; env "Testing"). Many overlap the existing `P1_05_RBAC_Tests.cs` — extend/validate that file rather than duplicating. |
| `docs/qc/P1-05/execution-report.md` | `api-tester` | After running, fill pass/fail per case + defects. QC scaffolds the empty template only; QC never fills results. |

`frontend-test-cases.md` is intentionally **not produced** — P1-05 has no student-app UI surface.
