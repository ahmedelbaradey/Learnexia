# Pipeline Brief — Phase 1 BACKEND → FRONTEND Coverage Gap Analysis

**Date:** 2026-05-24 · **Author:** analyzer agent · **Type:** read-only gap analysis (no code/migrations) · **Direction:** **backend capability → frontend consumer** (the reverse of `phase-1-design-gap-analysis.md`, which went FE design → BE).

## 0. Method & scope

For every Phase-1 **backend-exposed capability** (each endpoint / command / query a client can call — built in code, or "planned" in P1-12/P1-13/P1-13a tasks) I checked: does a **FE user story** cover it? Is there a **FE task**? Which screen consumes it? Status = **Covered / Partial / GAP (no FE) / N/A (no UI surface)**.

**Grounded in code, not just task prose.** Endpoints below were confirmed by grepping the Identity + Notifications modules:
- `AuthenticationController` (`api/Users/Authentication/...`): `Register-Parent`, `Sign-In`, `Validate-Token`, `Refresh-Token`, `Sign-Out`.
- `UsersController` (`api/Users/Me`): `GET Me`.
- `ParentController` (`api/Users/Parent/...`, role-gated Parent/Admin/SuperAdmin): `Add-Child`, `Link-Child`, `My-Children`.
- `UserManagementController` (`api/Users/UserManagement/[Action]`, **class-level AdminOnly**): UserList, GetUserById, AddUser, UpdateUser, UpdateUserLanguage, UserRoles, UpdateUserRoles, DeleteUser, ChangePasswordForUser, SetNewPasswordForUser, GetUserProfile, **UpdateUserProfile** (multipart — already accepts avatar), AdminResetPassword, ResendRegistrationMessage, CheckRoleAvailability, GetCurrentCultureLanguage, + fund-manager/board/legal list endpoints.
- `AuthorzationController` (`api/Users/Authorzation`, AdminOnly): role CRUD (RoleList, GetRoleById, AddRole, EditRole, DeleteRole, …).
- `NotificationsController`: admin/observability notification lookup only.
- **Confirmed ABSENT in code:** any self-service profile update, avatar self-upload, forgot/reset-password, OAuth/external-login, update-child. Only `AdminResetPassword` (admin sets another user's password) exists — there is no public forgot-password.

**Key framing correction up front:** the prior FE-design gap analysis already routed every design-implied backend gap into **P1-12 (Batch 2)**, and **P1-12-FE already plans the wiring** for profile-save, avatar, social-login, forgot-password, and edit-child. So those are **NOT FE gaps** — they have FE tasks (deferred to Batch 2). This brief therefore finds **far fewer FE gaps than one might expect**; the genuine ones are (1) the P1-13 sign-in **contract changes** (lockout / uniform errors) that no FE task accounts for, and (2) a few admin-dashboard and reachability items. See §C.

---

## A. Backend capability inventory (by story)

| Capability | Story / Task | Endpoint (verb + route) | What the user does |
|---|---|---|---|
| Register parent (email/pwd → JWT) | P1-01 / P1-01-BE | `POST api/Users/Authentication/Register-Parent` (built) | Parent creates their account |
| Sign-in (parent / child / admin) | P1-01,P1-10 / BE | `POST api/Users/Authentication/Sign-In` (built) | Any user logs in |
| Validate access token | P1-02 / BE | `POST api/Users/Authentication/Validate-Token` (built) | Client/infra validates a token |
| Refresh token | P1-02 / P1-02-BE | `POST api/Users/Authentication/Refresh-Token` (built) | Silent session renewal |
| Sign-out (revoke refresh) | P1-02 / P1-02-BE | `POST api/Users/Authentication/Sign-Out` (built, `[Authorize]`) | User ends session |
| Get current user (`Me`) | P1-09 / P1-09-BE-1 | `GET api/Users/Me` (built; returns Id/Roles/FullName/PreferredLanguage/IsFirstLogin/HasChildren) | App routes by role + onboarding + locale |
| Add child (provision + auto-link) | P1-03,P1-04 / BE | `POST api/Users/Parent/Add-Child` (built) | Parent provisions a child account |
| Link existing child | P1-04 / P1-04-BE | `POST api/Users/Parent/Link-Child` (built) | Parent links an already-provisioned child |
| List my children | P1-04 / P1-04-BE | `GET api/Users/Parent/My-Children` (built; id/fullName/email only) | Parent sees their linked children |
| Admin: user CRUD, roles, language, profile, admin-reset-password, resend-registration | P1-05,P1-10 (admin surface) | `api/Users/UserManagement/*`, `api/Users/Authorzation/*` (built, **AdminOnly**) | Admin manages users/roles (feature screens are Phase 2+/Backlog) |
| Admin: notification lookup | P4-01 (admin observability) | `NotificationsController` (built) | Admin inspects notifications (not P1 FE) |
| **Self profile read/update** (fullName, phone, country) | **P1-12a / P1-12-BE-1,2** | `GET`/`PUT` profile + enriched `/Me` — **planned, not built** | Parent edits own profile in Settings |
| **Avatar upload / remove** (self) | **P1-12b / P1-12-BE-4** | upload + remove endpoint — **planned, not built** | Parent sets/clears own photo |
| **Social login** (Google/Apple/Microsoft) | **P1-12c / P1-12-BE-5** | OAuth → same JWT/refresh — **planned, not built** | Parent signs in via provider |
| **Password reset** (forgot + set-new) | **P1-12d / P1-12-BE-6** | request-reset + set-new — **planned, not built** | Parent regains access (email link) |
| **Update / edit child** (fullName, grade, lang, country) | **P1-12e / P1-12-BE-8** | update-child (family-scope) — **planned, not built** | Parent edits a child after adding |
| **Register: capture country + terms-consent** | **P1-12f / P1-12-BE-9** | `RegisterParentCommand` + `country`/consent — **planned, not built** | Parent's country + consent persisted at sign-up |
| **Account lockout engaged** (sign-in contract change) | **P1-13 / P1-13-BE-1** | modifies `POST Sign-In` (planned) — locked account → localized message | Locked-out user sees a clear "account locked" message |
| **Sign-in safety** (uniform error, no enumeration) | **P1-13 / P1-13-BE-2** | modifies `POST Sign-In` (planned) — single generic "invalid credentials" | Login error becomes uniform (no user-not-found vs wrong-pwd branch) |
| **Anti-automation / CAPTCHA on register** | **P1-13 / P1-13-BE-6** | modifies `POST Register-Parent` (planned, config-gated) — may require a CAPTCHA token field | Register form may need a bot-challenge token |
| **Config-driven admin seed** | **P1-13 / P1-13-BE-5** | no endpoint — seed/config (planned) | Enables admin login with a real seeded account |
| **Email delivery infra** (`IEmailSender`) | **P1-13a / P1-13a-BE** | no endpoint — Notifications module (planned) | Sends welcome/registration + (later) reset emails |
| Postgres + pgvector + Redis | P1-06 / P1-06-BE | infra only | — |
| Docker / CI/CD / `/health` | P1-07 / P1-07-BE | `GET /health` (built) — infra | — |

---

## B. Frontend coverage matrix

| Backend capability | FE story? | FE task? | Screen / consumer | Status |
|---|---|---|---|---|
| Register parent | P1-09, P1-11d | P1-09-FE-2, **P1-11-FE-5** | Register page (`(auth)/register.tsx`) | **Covered** |
| Sign-in | P1-09, P1-10, P1-11c | P1-09-FE-3, **P1-11-FE-4**, **P1-10-FE-2** (admin) | Login pages (student + admin) | **Covered** |
| Validate token | — (infra) | api-client interceptor | — | **N/A** (used internally by api-client; no screen) |
| Refresh token | P1-02, P1-10 | P1-09 routing, **P1-10-FE-5** | api-client interceptor / admin shell | **Covered** |
| Sign-out | P1-10 | **P1-10-FE-5** (`useSignOut`); student logout implied | Admin shell; student app | **Partial** — admin covered; **no explicit student-app sign-out task** (see Gap F4) |
| `GET Me` | P1-09, P1-10, P1-11 | P1-09-FE-4, P1-10-FE-4, P1-11-FE-3 (header) | Route guard; dashboard header | **Covered** |
| Add child | P1-03, P1-09, P1-11e | P1-09-FE-2, **P1-11-FE-6**, P1-04-FE-1 | Add-child / My-Children | **Covered** |
| Link existing child | P1-04 | **P1-04-FE-2** (LinkChildForm) | Link-child form | **Covered** |
| List my children | P1-04, P1-11e | P1-04-FE-1, **P1-11-FE-6** | My Children | **Covered** |
| Admin user/role CRUD (`UserManagement`, `Authorzation`) | — | — | — | **N/A for P1** — admin P1 is auth + shell only (P1-10 says feature screens are Backlog/Phase 2+). Flag for later (see Gap F5). |
| Admin notification lookup | — | — | — | **N/A for P1** (Phase 4 admin observability) |
| Self profile read/update (P1-12a) | P1-12, P1-11h | **P1-12-FE-1** (+ P1-11-FE-10 UI-first) | Settings → Profile | **Covered** (Batch-2 deferred wiring) |
| Avatar upload/remove (P1-12b) | P1-12, P1-11h | **P1-12-FE-2** (+ P1-11-FE-14 Avatar built) | Settings → Profile avatar | **Covered** (Batch-2) |
| Social login (P1-12c) | P1-12, P1-11c | **P1-12-FE-3** (+ P1-11-FE-4 UI-only buttons) | Login social buttons | **Covered** (Batch-2; UI-disabled until BE) |
| Password reset (P1-12d) | P1-12 | **P1-12-FE-4** (`forgot-password.tsx`, `reset-password.tsx`) | Forgot/reset screens | **Covered** (Batch-2; UI-disabled until BE) |
| Update / edit child (P1-12e) | P1-12, P1-11e | **P1-12-FE-5** (+ P1-11-FE-7 EditChild) | EditChildSheet / edit-child route | **Covered** (Batch-2) |
| Register country + terms-consent (P1-12f) | P1-12, P1-11d | P1-11-FE-5 collects them; **wiring via P1-12-BE-9 / regen api-client** | Register form | **Partial** — UI collects `country`+`acceptedTerms`; no *explicit* FE wiring task ties them to BE-9 (see Gap F1) |
| **Account lockout message** (P1-13-BE-1) | — | — | Login pages (student + admin) | **GAP (no FE)** — see Gap F2 |
| **Sign-in uniform error** (P1-13-BE-2) | — | — | Login error handling | **GAP (no FE)** — see Gap F2 |
| **CAPTCHA on register** (P1-13-BE-6) | — | — | Register form | **GAP (no FE)** — see Gap F3 (low priority; BE itself may defer to P6) |
| Config-driven admin seed (P1-13-BE-5) | — | — | — | **N/A** (no UI; enables P1-10-FE-2 admin login to work) |
| Email delivery infra (P1-13a) | — | — | — | **N/A** (backend/infra; no UI. Powers welcome + reset emails consumed indirectly) |
| Email verification at registration | — | — | — | **N/A — BYPASSED by lead decision** (P1-13 "Lead decisions": no `RequireConfirmedEmail`, no confirm flow). No verify-email screen needed in P1. |
| Postgres/pgvector/Redis (P1-06) | — | — | — | **N/A** (infra) |
| Docker / CI / `/health` (P1-07) | — | — | — | **N/A** (infra/devops) |

---

## C. Gap list with recommendations

Only **four** items are genuine FE-coverage gaps; one is a tidy-up. None are large.

### F1 — Register `country` + `acceptedTerms` wiring is not explicitly tasked (Partial → recommend a task)
**What:** P1-11-FE-5 builds the Register form which *collects* `country` and a Terms checkbox, but the form today posts only `{email, password, fullName}` (per the HANDOFF + P1-12f). P1-12-BE-9 adds `country` + consent to `RegisterParentCommand`. **No FE task explicitly wires the collected fields to the new command** — P1-12-FE-1..6 covers profile/avatar/social/forgot/edit-child but **omits register country+consent**.
**Recommendation (a) — add one task to the existing P1-12-FE file:**
`tasks/Frontend/student-app/Phase-1-Foundation/P1-12-FE.md` → add **P1-12-FE-7 — Register country + consent wiring**: after `RegisterParentCommand` accepts `country` + terms-consent (P1-12-BE-9) and the api-client is regenerated, send the Register form's `country` + `acceptedTerms` (remove the client-only TODOs); handle validation errors. Dep: P1-11-FE-5, P1-12-BE-9.
**Justify:** P1-12-BE-9 is real planned backend with no FE consumer task; cheap (~2h) and keeps the matrix symmetric. Alternatively fold into P1-11-FE-5's acceptance criteria, but a Batch-2 wiring task is cleaner since BE-9 is Batch 2.

### F2 — Sign-in contract change (lockout message + uniform error) has no FE task — **the highest-value gap**
**What:** P1-13-BE-1/BE-2 **change the Sign-In response contract**: (1) a locked account returns a distinct localized (en/ar) "account locked" `BaseResponse` message after 5 failures; (2) wrong-email and wrong-password collapse into a **single generic "invalid credentials"** result (no enumeration). The P1-13-BE task file's own "Contract to Frontend" section says *"FE must not branch on user-not-found vs wrong-password … Locked-account returns its own localized message + (optionally) a retry-after hint."* **No FE story or task consumes this contract** — P1-11-FE-4 (Login) and P1-10-FE-2 (admin Login) predate P1-13 and assume the old error shapes.
**Recommendation (a) — add tasks to existing FE login tasks (no new story):**
- `tasks/Frontend/student-app/Phase-1-Foundation/P1-11-FE.md` → add **P1-11-FE-15 — Sign-in error/lockout handling**: render the uniform "invalid credentials" message (no user-not-found vs wrong-password branch) and a distinct **account-locked** message (localized en/ar, optional retry-after) on the redesigned Login; verify against `BaseResponse.Successed=false`. Dep: P1-11-FE-4, P1-13-BE-1/2.
- `tasks/Frontend/admin-dashboard/Phase-1-Foundation/P1-10-FE.md` → add **P1-10-FE-6 — Admin login lockout/uniform-error handling**: same uniform + locked-account messaging on the admin Login. Dep: P1-10-FE-2, P1-13-BE-1/2.
**Justify:** this is a backend behaviour change with a real user-facing consequence (a locked-out parent/admin must be *told* they're locked, not shown a generic error). Both login surfaces need it. Small (~2h each), no new story warranted.

### F3 — CAPTCHA token field on Register (low priority, may track with BE deferral)
**What:** P1-13-BE-6 can require a config-gated CAPTCHA token on `Register-Parent`; the FE must send a token only when the server advertises the requirement. No FE task accounts for it.
**Recommendation (a) — note + conditional task, do not build yet:**
Add a **note** to `P1-11-FE.md` (Register, FE-5) that *if* P1-13-BE-6 ships in P1, a CAPTCHA-token field (config-advertised) is required; otherwise track with the BE deferral. P1-13-BE itself flags BE-6 as possibly deferring to a P6 hardening pass. **Recommend: defer the FE work to match the BE decision** — do not create a task until the lead confirms BE-6 is in P1 scope. Effectively an **open question**, not a committed gap.

### F4 — Student-app sign-out has no explicit FE task (tidy-up)
**What:** `Sign-Out` is built (`POST api/Users/Authentication/Sign-Out`). Admin sign-out is tasked (P1-10-FE-5 `useSignOut`). The **student/parent app** has no explicit logout task in P1-09-FE / P1-11-FE — only implied by the route guard.
**Recommendation (a) — minor add or fold-in:**
Either add **P1-11-FE-16 — Sign-out affordance** (parent app header/settings → calls Sign-Out, clears authStore, routes to Login) to `P1-11-FE.md`, or confirm it's already covered by the existing `useSignOut`/authStore shared hook (HANDOFF suggests a shared api-client interceptor exists). **Recommend: confirm with the FE lead before adding** — likely already covered by the shared hook used by P1-10-FE-5, in which case this is N/A. Low priority.

### F5 — Admin-dashboard consumes none of the AdminOnly endpoints in P1 (correctly deferred — flag only)
**What:** `UserManagementController` + `AuthorzationController` expose a large AdminOnly surface (user CRUD, role CRUD, admin-reset-password, resend-registration). P1-10-FE builds **only** admin login + an empty shell with placeholders; none of these endpoints get an admin screen in P1.
**Recommendation:** **No action for P1 — correctly deferred.** P1-10's story explicitly scopes admin to "auth + shell only; feature screens live in the Backlog (BL-01..) and Phase 2+ / Phase 7 admin console (P7-xx)". Flag as a forward dependency for the Phase 7 Admin Console (already in the backlog per memory), **not** a P1 gap. Listed here so it isn't mistaken for an oversight.

---

## D. Open questions for the lead

1. **CAPTCHA on register (F3):** is P1-13-BE-6 in Phase-1 scope, or deferred to P6? The FE Register task should add a CAPTCHA-token field **only if BE-6 ships in P1**. (Recommend: defer both together.)
2. **Sign-in contract change (F2):** confirm both Login surfaces (student `P1-11-FE-4` + admin `P1-10-FE-2`) should be updated for the uniform-error + lockout-message contract. Sequencing: these FE tweaks must land **after** P1-13-BE-1/2 merge (they depend on the new response shape).
3. **Student-app sign-out (F4):** is logout already covered by the shared `useSignOut`/api-client hook (the one P1-10-FE-5 reuses), or does the parent app need its own task? Confirm to close or keep F4.
4. **Admin-dashboard P1 scope (F5):** confirm admin stays **auth + shell only** for Phase 1 (no UserManagement/role screens until Phase 7 P7-xx). Assumed yes per P1-10 story.
5. **Register country+consent wiring (F1):** put the wiring in a new **P1-12-FE-7** (Batch 2, recommended) or fold into P1-11-FE-5 now? Since BE-9 is Batch 2, recommend the P1-12-FE-7 home.
6. **Email-verification bypass (confirmed, no UI):** noting for the record — the lead decision in `P1-13-backend-hardening.md` BYPASSES email verification, so **no verify-email screen** is needed in P1. Flag if that decision reverses (it would add a FE screen + a verify endpoint).

---

## E. Summary

- **Most backend capabilities are FE-covered.** The earlier FE-design gap analysis already pushed the design-implied backend (profile, avatar, OAuth, forgot-password, edit-child) into **P1-12**, and **P1-12-FE already plans the wiring** (FE-1..FE-5). Those are *not* gaps — they're deferred Batch-2 tasks.
- **Genuine FE gaps are small and concentrated on the P1-13 sign-in contract change:**
  - **F2 (real, do it):** lockout message + uniform sign-in error → add **P1-11-FE-15** (student) and **P1-10-FE-6** (admin). The BE contract change has no FE consumer today.
  - **F1 (recommended):** register country+consent wiring → add **P1-12-FE-7**.
  - **F3 (open question):** CAPTCHA field — defer with the BE decision; no task yet.
  - **F4 (verify):** student-app sign-out — likely already covered by the shared hook; confirm.
- **N/A (no UI surface), justified:** P1-06 (DB infra), P1-07 (docker/CI/`/health`), P1-13a (email send), P1-13-BE-5 (admin seed config), email verification (BYPASSED), the AdminOnly UserManagement/Authorzation surface (deferred to Phase 7), and `Validate-Token` (internal to api-client).
- **No new FE user story is needed.** Every real gap fits as a task added to an existing P1 FE task file (P1-11-FE, P1-10-FE, or P1-12-FE).

## Relevant file paths (absolute)
- This brief: `e:\Wrokspace\Learnexia\docs\briefs\phase-1-frontend-coverage-gap-analysis.md`
- Prior briefs (context): `e:\Wrokspace\Learnexia\docs\briefs\phase-1-design-gap-analysis.md`, `phase-1-backend-gap-analysis.md`, `barrier-to-entry-gap-analysis.md`
- FE tasks to amend: `e:\Wrokspace\Learnexia\tasks\Frontend\student-app\Phase-1-Foundation\P1-11-FE.md`, `P1-12-FE.md`; `e:\Wrokspace\Learnexia\tasks\Frontend\admin-dashboard\Phase-1-Foundation\P1-10-FE.md`
- BE source of the contract changes: `e:\Wrokspace\Learnexia\backend\src\Modules\Identity\Learnexia.Modules.Identity.Application\Features\Authentications\Commands\SignIn\SignInCommandHandler.cs`
- Built endpoints: `e:\Wrokspace\Learnexia\backend\src\Modules\Identity\Learnexia.Modules.Identity.Api\Controllers\` (`AuthenticationController`, `UsersController`, `ParentController`, `UserManagementController`, `AuthorzationController`)
