# Pipeline Brief — P1-10 Admin sign-in & dashboard shell

> Analyzer output. Read-only brief; the rest of the pipeline (planner → designer → frontend / backend-feature → api-tester → security-auditor → reviewer → committer) executes against this. Source of truth = the user story `user-stories/Phase-1-Foundation/P1-10-admin-sign-in.md` (acceptance criteria) + the BE/FE task files. Honors the CLAUDE.md product overrides (no teacher role; parent-driven onboarding; **admin is a real role**).

## Summary & traceability

- **Task (1 line):** Let an admin sign in to a secure **Next.js `admin-dashboard`** app (JWT from the existing Identity module), land on an authenticated **dashboard shell** with placeholder nav, with admin accounts **seeded/invited only** (no public self-registration) and non-admins denied admin routes. Auth + shell only — no admin feature logic.
- **User story:** P1-10 *"Sign in to the admin dashboard"* (3 pts, Story, Phase 1 — Foundation / Week 1–2, **Identity & Onboarding** epic).
- **SRS / FR IDs:** **SRS §3** (Admin role), **FR-ID-1** (authentication / sign-in), **FR-ID-4** (role-based access), **NFR-4** (security).
- **BRD goal:** Indirect — supports **G5** (scalable, manageable platform / operations). Admin is an operator surface; the curriculum/content features that deliver student value (G1–G4) arrive in Phase 2+/Backlog (BL-01..BL-05).
- **Phase/sprint:** Phase 1 — Foundation. Builds on P1-01/P1-02 (Identity + JWT), P1-05 (RBAC / Admin role + policies), P1-06 (DB), P1-08 (design-system), P1-09 (`Me`), and the monorepo skeleton (P1-07 / PKG-FE-1).

## Business context & value

- **Who benefits:** the **admin** (operator) — the only person who manages curriculum/subjects/content moderation. No student/parent value directly.
- **Value:** establishes the *access door + shell* for all future admin tooling. It is deliberately thin: pure reuse of the Identity module + RBAC, plus a seeded admin account, an admin-only guard, and the first Next.js app in the monorepo. Net-new code is small; the cost is the dashboard scaffolding.
- **Success measurement:** an admin can sign in and reach the shell; a non-admin cannot; refresh + sign-out work; the shell renders placeholder nav with no feature logic.

## Acceptance criteria (reviewer gates)

1. Admin signs in with valid email/password → JWT issued → lands on dashboard shell. (BE-2 + FE-2/FE-3)
2. Admin accounts are **seeded/invited only** — there is **no public admin registration path** (consistent with SRS §3 and the no-self-register product decision). Only a Login page exists in the admin app. (BE-1 + FE-2)
3. Non-admin (or unauthenticated) credentials → admin routes denied: backend returns **403** on admin-guarded endpoints; frontend redirects to login. (BE-3 + FE-4)
4. Session **refresh** and **sign-out** work for admins (reuse Identity P1-02 + the api-client interceptor). (reuse P1-02-BE + FE-5)
5. The dashboard shell renders **authenticated nav with placeholders** for later admin features (curriculum upload, content management) — **no feature logic yet**. (FE-3)
6. `Me` returns `role = Admin` so the dashboard can gate routes. (BE-4, reuse P1-09)

## Affected modules & data

**Backend — Identity module (existing; reuse-heavy):**

| Surface | New vs existing | Notes |
|---|---|---|
| Admin role | **Exists** — `RoleSeeder.cs` already seeds `Roles.Admin` (and `SuperAdmin`). | `Identity.Infrastructure/Persistence/Seed/RoleSeeder.cs` |
| Seeded admin account | **Partly exists** — `UserSeeder.SeedSuperAdminAsync` already creates a `superadmin` user in `Admin` + `SuperAdmin` roles. BE-1 wants an idempotent **Admin** account (and to confirm/own the admin seed). Likely an edit to `UserSeeder.cs` (add/confirm a dedicated Admin user) — **no new entity**. | `Identity.Infrastructure/Persistence/Seed/UserSeeder.cs` |
| Sign-In | **Exists, reuse as-is** — `POST /api/Users/Authentication/Sign-In` → `SignInCommand` → `BaseResponse<JwtAuthResponse>`. BE-2 only *confirms* the JWT carries the Admin role/claims; no code change expected. | `AuthenticationController.cs`, `SignInCommandHandler` |
| `Me` | **Exists, reuse** — current-user query returns role; BE-4 confirms it returns `Admin`. | `Features/Users/Queries/...` (P1-09) |
| Admin-only authorization | **New usage of existing infra** — apply `[Authorize(policy)]` (RBAC policies from P1-05, registered in `Identity.Infrastructure/DependencyInjection.cs` via `Claims.GenerateModules()`/`GeneratePermissions()`). For P1-10 there are **no admin feature endpoints yet**, so BE-3 is mostly establishing the guard pattern (and possibly a role/policy check used by `Me`-driven FE gating). | `[Authorize(...)]` attributes on (future) admin controllers |

No new tables, no migration. (Roles/users are seeded into the existing Identity schema.)

**Frontend — `apps/admin-dashboard` (Next.js 15, brand new):**

| Surface | New? | Notes |
|---|---|---|
| `apps/admin-dashboard` Next.js 15 App-Router app | **NEW — does not exist yet** | The monorepo `apps/` and `packages/` folders are **not populated on disk** (verified: no `apps/`, no `frontend/apps/`, no `packages/`). This is the **first app to land in `apps/admin-dashboard`**. Scaffolding includes Tamagui via `@tamagui/next-plugin` consuming `packages/design-system`. |
| Login page | NEW | `app/login/page.tsx` wired through `@learnexia/api-client` Sign-In; stores JWT in `authStore`. |
| Dashboard shell | NEW | `app/(admin)/layout.tsx` + `AdminShell` with side nav placeholders. |
| Route guard | NEW | `middleware.ts` + `useAdminGuard` gating on `Me.role === Admin`. |
| Session refresh + sign-out | NEW (reuses shared interceptor) | `useSignOut`; reuses `packages/api-client` JWT/refresh interceptor. |

**Frontend package dependencies (must exist first):** `packages/design-system` (P1-08), `packages/ui` (P1-08), `packages/api-client` (PKG-FE-3 — Sign-In hook + JWT/refresh interceptor + `BaseResponse<T>` handling), `packages/shared` (PKG-FE-? — `authStore`, `Roles` constant, types). Per FRONTEND_ARCHITECTURE §7 build order, these precede any app. The FE task deps confirm this: FE-1 depends on PKG-FE-1 + P1-08-FE; FE-2/FE-5 depend on PKG-FE-3.

## Handoff → db-migration

- **None.** No new entities/tables. Admin role + admin user are **seed data** in the existing Identity schema (applied by `IdentitySeeder` which already runs `MigrateAsync()` + role/user seeders, invoked from `Program.cs`). The db-migration agent is **not required** for this story.

## Handoff → backend-feature

- **BE-1 (seed admin, idempotent):** edit `UserSeeder.cs` (and confirm `RoleSeeder.cs` already has `Admin`). Ensure a dedicated, idempotent **Admin** account exists with the `Admin` role; keep the existing idempotency guard pattern (`FindByEmailAsync` null-check). No public admin registration path is added anywhere. Conventions: keep `ILoggerManager`, `BaseResponse`/`Successed` where envelopes apply.
- **BE-2 (confirm sign-in):** verify `SignInCommandHandler` issues a JWT carrying the Admin role/claims for the seeded admin. Expected **no code change** — reuse `POST /api/Users/Authentication/Sign-In`.
- **BE-3 (admin-only guard):** establish the `[Authorize(policy)]` pattern for admin-only endpoints using the P1-05 RBAC policies; non-admin → 403. Since no admin **feature** endpoints exist yet, this may be limited to the guard pattern + a smoke endpoint or documenting the policy to use. Flag to planner: confirm whether a representative guarded endpoint is in scope or deferred to BL-01.
- **BE-4 (`Me` returns Admin):** confirm the current-user query returns `role = Admin`. Reuse P1-09; expected no change.
- **Contract for FE:** Sign-In → `BaseResponse<JwtAuthResponse>`; `Me` → `role = Admin`. Same envelope as the student app.

## Handoff → frontend

- **Scaffold `apps/admin-dashboard`** (Next.js 15, App Router) in the Turborepo monorepo with `@tamagui/next-plugin` consuming `packages/design-system`. This is greenfield — the app does not exist.
- **Login page** (`app/login/page.tsx`) → `@learnexia/api-client` Sign-In hook → store JWT in `authStore` (`packages/shared`).
- **Dashboard shell** (`app/(admin)/layout.tsx` + `AdminShell`): app layout + side nav with placeholders (curriculum upload, content management) — no feature logic.
- **Route guard** (`middleware.ts` + `useAdminGuard`): unauthenticated or non-admin → redirect to login; gate on `Me.role === Admin`.
- **Refresh + sign-out** (`useSignOut`) reusing the `api-client` interceptor.
- **Design stage required** (UI surface): the `designer` produces a Design Spec for the admin login + shell (kit in `design-system/`, dark game-world palette/tokens) before the frontend batch. Note the admin dashboard is data-dense (TanStack Table + Recharts on Tamagui primitives per FRONTEND_ARCHITECTURE §5) — but P1-10 only needs login + shell.
- **Security-sensitive (auth/authz):** `security-auditor` should audit the FE auth flow (token storage on web, guard bypass) and the BE admin-guard before the gate.

## Parallelism vs P4-01

**Context:** P4-01 (domain-events backbone) runs concurrently and will modify the **Identity** module and **Host/Program.cs** (unify MediatR + add a `UserRegistered` producer). Decision-check is `docs/dev/PARALLELISM.md` §"What can run in parallel".

### Exactly what P1-10 **backend** touches

P1-10 backend is **pure reuse** of the existing Identity module — it does **not** add new endpoints or a registration flow. Concretely:

| P1-10 BE task | File(s) touched | Shared with P4-01? |
|---|---|---|
| BE-1 seed admin | `Identity.Infrastructure/Persistence/Seed/UserSeeder.cs` (edit), `RoleSeeder.cs` (read/confirm — Admin role already seeded) | **Same module (Identity), different files** than P4-01's Identity edits. |
| BE-2 confirm sign-in | none (reuse `AuthenticationController` / `SignInCommandHandler` as-is) | No edit. |
| BE-3 admin-only guard | `[Authorize(policy)]` on (future) admin endpoints — no admin feature endpoints exist yet in P1-10 | No overlap with P4-01. |
| BE-4 `Me` returns Admin | none (reuse P1-09 current-user query) | No edit. |

So **P1-10 BE's only real write is the seeder** (`UserSeeder.cs`). It reuses the existing `AuthenticationController` sign-in flow + the Admin role/policy from P1-05 — **no new endpoints, no new registration**.

### Exactly what P4-01 touches in Identity + Host (from `docs/plans/P4-01-domain-events-backbone.md`)

- **`Host/Program.cs`** — P4-01-BE-4 unifies cross-module MediatR registration (scans all module Application assemblies) and registers the custom `INotificationPublisher`. **Shared-file edit.**
- **`Identity.Application`** — P4-01-BE-6 adds a `UnitOfWorkBehavior` instance + registers it in `Identity.Application/DependencyInjection.cs`; P4-01-BE-8 makes the user-registration path raise `UserRegisteredIntegrationEvent`. These touch Identity **Application** files (DI + a command handler), **not** the Infrastructure seeders.

### Frontend disjointness

**P1-10 frontend is fully disjoint from P4-01.** P4-01 is a backend-only technical enabler with an explicit **"Handoff → frontend: None"** (brief line 138-139). P1-10 FE creates a brand-new `apps/admin-dashboard` Next.js app + consumes shared packages. **Zero file overlap** — safe to run the FE track entirely in parallel.

### Shared-file collision analysis

- **`Host/Program.cs`** — P4-01 **edits** it (unify MediatR). P1-10 **does not need to edit** it: the Identity seed hook (`IdentityModule.SeedAsync`) is *already* invoked in `Program.cs` (lines 85-88), and P1-10 adds no new module/registration. **So P1-10 should not touch Program.cs.** If BE-3 ends up adding a representative admin controller, that controller is discovered via the existing Identity application-part registration — still no Program.cs edit. → **No `Program.cs` collision expected**, *provided P1-10 BE refrains from any Program.cs change* (planner should enforce this).
- **`Identity` module** — both stories live in the Identity module, but in **different projects/files**: P1-10 = `Identity.Infrastructure/.../Seed/UserSeeder.cs`; P4-01 = `Identity.Application/DependencyInjection.cs` + a registration command handler. **No same-file edit.** However, per PARALLELISM.md golden rule #2 ("one working tree = one pipeline") and the decision-check ("they edit disjoint **module folders**"), the *module* is shared even though the *files* differ — git merges cleanly for disjoint files, but two pipelines must each run in their **own worktree/branch** and integrate in dependency order.
- **`Claims.GenerateModules()` / `Directory.Packages.props` / `.sln`** — P1-10 BE touches **none** of these (admin reuses existing policies; no new packages on the BE side; no new project). P4-01 likewise does not modify Claims. → no collision on the canonical shared-file list.

### Verdict

| Part of P1-10 | Verdict vs P4-01 | Reason |
|---|---|---|
| **Frontend** (`apps/admin-dashboard` scaffold, login, shell, guard, sign-out) | **SAFE to run in parallel** (own worktree/branch) | Fully disjoint — P4-01 has no FE surface; new Next.js app + shared packages, zero shared files. |
| **Backend BE-1** (seed admin in `UserSeeder.cs`) | **SAFE in parallel** — but **must be its own worktree** and merged in dependency order; **trivially mergeable** with P4-01 (different files in the Identity module). | Same module, **different file** from P4-01's Identity edits; no `Program.cs` edit required. |
| **Backend BE-2 / BE-4** (confirm sign-in / `Me`) | **SAFE — no edits** | Pure verification of existing reuse. |
| **Backend BE-3** (admin-only guard) | **SAFE in parallel** *iff* it does **not** edit `Program.cs` and adds no new MediatR registration | Uses existing P1-05 policies + existing application-part discovery. |

**Bottom line:** P1-10 is **safe to run in parallel with P4-01** in its own `feat/P1-10-admin-sign-in` worktree. There is **no hard serializer** between them, because P1-10 BE does **not** edit the two files P4-01 owns (`Host/Program.cs`, `Identity.Application/DependencyInjection.cs` + the registration handler) — its only BE write is `UserSeeder.cs`.

**The one guardrail (planner must enforce):** keep P1-10 BE out of `Host/Program.cs` and out of `Identity.Application` DI/MediatR registration. If, during implementation, BE-3 turns out to need a `Program.cs` change (e.g. global admin authorization policy wiring) **or** any MediatR re-registration, that specific change becomes a **serialized shared-file edit** and must be applied after P4-01 merges (or on `main` between merges) per PARALLELISM.md rule #3. As scoped today (reuse-only, no Program.cs edit), no serialization is required.

**Merge order:** independent siblings; merge whichever finishes first, rebase the second on `main`, re-run build + reviewer after any shared-touch merge.

## Open questions / assumptions / risks

**Open questions for the lead/user:**
1. **Admin vs SuperAdmin seed:** `UserSeeder` already seeds a `superadmin` (in `Admin`+`SuperAdmin` roles) with a hardcoded dev password `123Pa$$word!`. Does BE-1 want a *separate* dedicated `admin` account, or is confirming the existing `superadmin` (which has the Admin role) sufficient? And what is the real admin email/credential provisioning policy (NFR-4) — the hardcoded dev password must not ship to prod.
2. **BE-3 scope:** there are **no admin feature endpoints** in P1-10 (features are Backlog BL-01..). Is BE-3 just establishing the `[Authorize(policy)]` pattern + an FE-side guard, or should it add a representative guarded admin endpoint (e.g. an admin-only `ping`) so the 403 path is testable by api-tester? This affects whether api-tester has an HTTP surface to validate.
3. **Token storage on web:** FRONTEND_ARCHITECTURE §6 + §9 flag web token storage (secure cookie vs web storage) as open. The admin app needs a decision (secure cookie recommended for a Next.js middleware guard).
4. **Which admin policy:** confirm the exact P1-05 policy/role string the admin guard checks (role `Admin` vs a specific `{Module}.{Action}` permission). The story gates the FE on `Me.role === Admin`.

**Assumptions (proceed unless overridden):**
- A1: No DB migration (seed-only into existing Identity schema).
- A2: P1-10 BE does **not** edit `Host/Program.cs` or `Identity.Application` DI — keeping it parallel-safe with P4-01.
- A3: Sign-In and `Me` work as-is and need confirmation only, not code changes.
- A4: The monorepo skeleton + `packages/{design-system,ui,api-client,shared}` exist (or land first) — P1-10 FE is the **first** `apps/` consumer; if the skeleton/packages are not yet merged, P1-10 FE is **blocked on them**, not on P4-01.

**Risks:**
- **R1 (med):** Frontend package prerequisites (P1-07 monorepo skeleton, P1-08 design-system/ui, PKG-FE-3 api-client) may not be merged yet — none exist on disk today. P1-10 FE **cannot scaffold** until they do. This is the real schedule dependency, *not* P4-01.
- **R2 (low):** Scope creep on BE-3 into `Program.cs`/MediatR would convert P1-10 from parallel-safe to a serialized shared-file edit vs P4-01. Keep the guard reuse-only.
- **R3 (low/security):** Hardcoded seed password + `RequireHttpsMetadata=false` are dev conveniences; flag for security-auditor that prod admin provisioning must use real credentials/invite + HTTPS.
- **R4 (low):** `RoleSeeder` seeds many legacy/irrelevant roles (FundManager, LegalCouncil, BoardMember…) ported from another product. Out of scope for P1-10, but note the Admin role itself is present and correct.

## Recommended pipeline order (first cut — planner finalizes)

```
0. Prereq check: confirm monorepo skeleton (P1-07) + packages design-system/ui/api-client/shared
   are merged. If not, P1-10 FE is blocked on them (not on P4-01).

1. analyzer (this brief) → planner.

2. Two independent tracks (run in parallel, each in feat/P1-10 worktree — Mode A within the story):
   Track BE (backend-feature):
     - BE-1 seed admin (edit UserSeeder.cs only)   [reviewer-gate]
     - BE-2 confirm sign-in, BE-4 confirm Me        [no/low code]
     - BE-3 admin-only guard pattern (no Program.cs edit)
     → api-tester IF a guarded endpoint exists (story-dependent)
     → security-auditor (auth/authz)
   Track FE:
     - designer: Design Spec for admin login + shell
     - FE-1 scaffold apps/admin-dashboard (Next.js 15 + Tamagui)
     - FE-2 login page → api-client Sign-In → authStore
     - FE-3 dashboard shell + placeholder nav
     - FE-4 route guard (Me.role === Admin)
     - FE-5 refresh + sign-out
     → security-auditor (token storage, guard bypass)

3. reviewer gates each track against the acceptance criteria above.

4. committer (after reviewer PASS) on feat/P1-10-admin-sign-in.
```

**Clear to plan?** **Yes — clear to plan**, with the four open questions ideally answered first (esp. Q1 admin-seed policy and Q2 BE-3 endpoint scope, which shape api-tester applicability). No hard blocker against P4-01: P1-10 is parallel-safe in its own worktree as long as BE stays out of `Program.cs`/`Identity.Application` MediatR registration. The genuine prerequisite to watch is the **frontend monorepo + packages** (R1/A4), which must be merged before the FE track can scaffold.
