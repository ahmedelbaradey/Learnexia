# Execution Plan — Phase 1 Backend Leftover (P1-13a · P1-13 · P1-12)

**Written:** 2026-05-24 · **Author:** planner agent
**Scope:** Three remaining Phase 1 backend stories — all Identity + Notifications modules, no frontend work.

---

## Source

| Artifact | Path |
|---|---|
| Brief (backend gap analysis) | `docs/briefs/phase-1-backend-gap-analysis.md` |
| Brief (design gap analysis) | `docs/briefs/phase-1-design-gap-analysis.md` |
| Story P1-13a | `user-stories/Phase-1-Foundation/P1-13a-notifications-email-delivery.md` |
| Story P1-13 | `user-stories/Phase-1-Foundation/P1-13-backend-hardening.md` |
| Story P1-12 | `user-stories/Phase-1-Foundation/P1-12-web-account-backend-batch2.md` |
| Tasks P1-13a | `tasks/Backend/Phase-1-Foundation/P1-13a-BE.md` |
| Tasks P1-13 | `tasks/Backend/Phase-1-Foundation/P1-13-BE.md` |
| Tasks P1-12 | `tasks/Backend/Phase-1-Foundation/P1-12-BE.md` |
| Rules | `CLAUDE.md`, `docs/dev/CONVENTIONS.md`, `docs/dev/FEATURE_PLAYBOOK.md`, `docs/dev/adr/0001-unit-of-work.md` |
| Handoff | `docs/dev/HANDOFF.md` |

---

## Task inventory

| ID | Story | Stack | Summary | Est (h) | Depends on | Ask-first gate? |
|---|---|---|---|---|---|---|
| **P1-13a-BE-1** | P1-13a | backend-feature | Define `IEmailSender`; implement `SendNotificationCommandHandler` against it; one real provider adapter (config-driven) + dev no-op/log sink; secrets from env only | 6 | P1-06-BE | **YES — IEmailSender Adapter pattern + staging provider choice must be named and approved before coding** |
| **P1-13a-BE-2** | P1-13a | backend-feature | Extend `UserRegisteredIntegrationEventHandler` to send welcome email via BE-1, best-effort, failure isolated from registration; keep existing notification-row write | 3 | P1-13a-BE-1 | No |
| **P1-13a-BE-3** | P1-13a | security-auditor | Audit: email secrets handling, header/content injection, SSRF/open-relay, failure-path info leakage | — | P1-13a-BE-1, BE-2 | No |
| **P1-13-BE-1** | P1-13 | backend-feature | Engage account lockout: switch `SignInCommandHandler` to `lockoutOnFailure: true`; surface locked-account as localized `BaseResponse` (en/ar); confirm options reset on success | 3 | P1-02-BE | No |
| **P1-13-BE-2** | P1-13 | backend-feature | Sign-in safety: stop returning raw `ex.Message`; collapse `NotFound`/`BadRequest` into a single "invalid credentials" result (no account-enumeration oracle); log detail server-side | 2 | P1-13-BE-1 | No |
| **P1-13-BE-3** | P1-13 | backend-feature | Config-driven Admin seed: idempotent seed reading email/password from env; remove/guard legacy hardcoded `123Pa$$word!` default in non-Development; ensure Admin role exists | 3 | P1-01-BE-2, P1-06-BE | No |
| **P1-13-BE-4** | P1-13 | backend-feature | Anti-automation CAPTCHA hook on Register-Parent: pluggable verifier abstraction, config-gated (no-op in dev/tests); keep existing IP rate-limit | 4 | P1-01-BE | **YES — CAPTCHA verifier abstraction pattern must be named and approved; may defer to Phase 6** |
| **P1-13-BE-5** | P1-13 | security-auditor | Audit: auth-path changes (lockout/enumeration BE-1/BE-2), admin-credential seed (BE-3), bot-challenge (BE-4) | — | BE-1, BE-2, BE-3, BE-4 | No |
| **P1-12-BE-3** | P1-12 | db-migration | Migration: add `Phone`, `Country`, `AvatarUrl` columns to Identity `User`; Npgsql, identity schema — **single serialized migration for all three P1-12 features that touch User** | 2 | — | No |
| **P1-12-BE-1** | P1-12 | backend-feature | Profile read/update: `GET` profile + update command (fullName, phone, country); `BaseResponse<T>`, `[Authorize]` self; `ValidationBehavior` (ICommand) | 5 | P1-12-BE-3, P1-05-BE | No |
| **P1-12-BE-2** | P1-12 | backend-feature | Enrich `/Me`: add fullName, phone, country (+ avatarUrl once BE-4) to the `Me` projection | 2 | P1-12-BE-1 | No |
| **P1-12-BE-8** | P1-12 | backend-feature | Update/edit child: update command (fullName, grade, preferredLanguage, country); family-scope authz (own child only); returns updated child; `ValidationBehavior`; regenerate `api-client` `updateChild` | 4 | P1-04-BE, P1-05-BE | No |
| **P1-12-BE-9** | P1-12 | backend-feature | Register: add `country` to `RegisterParentCommand` (validate + store, reuses BE-3 column); store terms-consent record (bool + timestamp) at registration for COPPA auditability; regenerate api-client | 3 | P1-12-BE-3 | No |
| **P1-12-BE-4** | P1-12 | backend-feature | Avatar upload/remove: endpoint (type/size validation, safe storage); set/clear `AvatarUrl`; file-storage decision (dev-local vs object store) | 6 | P1-12-BE-3 | **YES — file-storage abstraction pattern (dev-local vs object store) must be named and approved** |
| **P1-12-BE-5** | P1-12 | backend-feature | OAuth (Google/Apple/Microsoft): provider sign-in → same JWT/refresh as password login; link/create parent account | 6 | P1-02-BE (tokens) | **YES — OAuth provider abstraction pattern must be named and approved; provider credentials required** |
| **P1-12-BE-6** | P1-12 | backend-feature | Password reset: request (email link, no enumeration) + set-new-password (token, policy, invalidate other sessions); wires P1-11 "Forgot password?" | 5 | P1-02-BE, P1-13a-BE-1 (email) | No |
| **P1-12-BE-7** | P1-12 | security-auditor | Audit: avatar upload (BE-4) + OAuth/secrets (BE-5) + password reset (BE-6) | — | BE-4, BE-5, BE-6 | No |

**Total estimated implementation hours:** ~54 h (excluding security-auditor and reviewer passes).

---

## Dependency graph

```
P1-06-BE (DB/infra — already merged)
    └── P1-13a-BE-1 [GATED]
            └── P1-13a-BE-2
                    └── P1-13a-BE-3 [security-auditor]
                            └── P1-13a reviewer gate
                            └── (unblocks P1-12-BE-6)

P1-02-BE (auth tokens — already merged)
    ├── P1-13-BE-1
    │       └── P1-13-BE-2
    └── P1-12-BE-5 [GATED]
            └── P1-12-BE-7 (partial)

P1-01-BE-2 (role/seed bootstrap — already merged)
P1-06-BE
    └── P1-13-BE-3

P1-01-BE (register parent — already merged)
    └── P1-13-BE-4 [GATED]

P1-13-BE-1, P1-13-BE-2, P1-13-BE-3 → P1-13-BE-5 [security-auditor]
    (P1-13-BE-4 also feeds BE-5 if approved and built in this pass)

Identity schema migration (P1-12-BE-3) — SINGLE SERIALIZED MIGRATION:
    └── P1-12-BE-1 (profile read/update)
    │       └── P1-12-BE-2 (enrich /Me)
    └── P1-12-BE-9 (register country + consent)
    └── P1-12-BE-4 [GATED] (avatar upload — also needs BE-3 AvatarUrl col)

P1-04-BE, P1-05-BE (family/RBAC — already merged)
    └── P1-12-BE-8 (edit child — no schema change needed)

P1-13a-BE-1 (email infra, approved+built) + P1-02-BE
    └── P1-12-BE-6 (password reset)
            └── P1-12-BE-7 (security-auditor, together with BE-4 + BE-5)
```

**Critical paths:**
- P1-13a approval → P1-13a-BE-1 → P1-13a-BE-2 → security-auditor → P1-12-BE-6 (password reset)
- P1-12-BE-3 migration must run before BE-1/BE-2/BE-4/BE-9 can code against the new columns

---

## Execution batches

All batches run on branch `feat/<StoryID>-<slug>` (one branch per story). Where batches are parallel-safe, the lead dispatches them simultaneously in independent git worktrees (per PARALLELISM.md). Shared-file serialization rules are called out per batch.

---

### Batch 0 — Ask-first gates (no implementation; lead resolves before dispatching Batches 1/2)

**Agent:** lead / user decision — no implementer dispatched yet.

These four tasks are blocked on pattern approval per CLAUDE.md rule #8:

| Task | Decision needed |
|---|---|
| P1-13a-BE-1 | Approve the `IEmailSender` + Adapter design (minimal: single adapter + dev no-op, no Strategy until 2nd provider is real). Separately: choose the staging/prod email provider (SMTP relay vs SendGrid/SES/etc.). **This is the longest-lead decision** — it gates the entire password-reset path. |
| P1-12-BE-4 | Approve the file-storage abstraction for avatar upload (proposed: `IFileStorage` interface + dev-local adapter for now, leaving object-store swap-in for a later PR). |
| P1-12-BE-5 | Approve the OAuth provider abstraction pattern (proposed: one concrete adapter per provider; no dynamic provider-selection Strategy until warranted). Also confirm provider credentials are available for at least one provider (Google). |
| P1-13-BE-4 | Decide whether CAPTCHA lands now or defers to Phase 6 (gap analysis recommends defer). If now: approve the pluggable verifier abstraction. |

Once approved, note the decision in `docs/dev/HANDOFF.md` and dispatch the relevant batches.

---

### Batch 1a (parallel track — P1-13a enabler, starts after Batch 0 P1-13a approval)

**Branch:** `feat/P1-13a-notifications-email`
**Agents:** `backend-feature`

| Task | Agent | Notes |
|---|---|---|
| P1-13a-BE-1 | backend-feature | Notifications module only (`SendNotificationCommandHandler`, `IEmailSender`, adapter, DI wire in `AddNotificationsInfrastructure`). Secrets from env/`appsettings.{env}.json` never committed. No cross-module FK. No UoW (Notifications module already uses `SaveChangesAsync` directly in the integration event handler — same pattern). |
| P1-13a-BE-2 | backend-feature | Extend `UserRegisteredIntegrationEventHandler`; failure must be caught and logged — must NOT propagate and fail the registration flow. |

**Shared-file touches:** `Notifications.Infrastructure/DependencyInjection.cs` (register `IEmailSender` impl). No `Program.cs` change needed (Notifications module already wired). No Identity migration.

**After Batch 1a:** run `security-auditor` (P1-13a-BE-3) then `reviewer` gate before merging.

---

### Batch 1b (parallel track — P1-13 hardening, starts immediately with no gate on BE-1/BE-2/BE-3)

**Branch:** `feat/P1-13-backend-hardening`
**Agents:** `backend-feature`

| Task | Agent | Sequential order | Notes |
|---|---|---|---|
| P1-13-BE-1 | backend-feature | First | Identity `SignInCommandHandler` only. Change `lockoutOnFailure: false` → `true`. Add localized `BaseResponse` for the locked-account case. |
| P1-13-BE-2 | backend-feature | After BE-1 | Same file (`SignInCommandHandler`). Collapse `NotFound`/`BadRequest` → single "invalid credentials". Stop returning raw `ex.Message`. Sequential because BE-2 modifies the same handler as BE-1. |
| P1-13-BE-3 | backend-feature | Parallel to BE-1/BE-2 | `UserSeeder.cs` + new config section. Does NOT touch `SignInCommandHandler` — can run in parallel if a second implementer is available. Guard hardcoded password in non-Development. |

**Shared-file touches:** `SignInCommandHandler.cs` (BE-1 + BE-2 must be sequential on this one file); `UserSeeder.cs` (BE-3, independent). `appsettings.json` or `appsettings.Development.json` for seed config section (no secrets committed). Identity `DependencyInjection.cs` unlikely to need changes.

**P1-13-BE-4 (CAPTCHA):** do NOT start until Batch 0 approves it. If deferred to Phase 6, it is dropped from this batch and the security-auditor (BE-5) runs without it.

**After Batch 1b (BE-1/BE-2/BE-3 done):** run `security-auditor` (P1-13-BE-5; covers BE-1/BE-2/BE-3, and BE-4 if built) then `api-tester` (sign-in endpoint behaviour changed), then `reviewer` gate.

---

### Batch 1c (parallel track — P1-12 schema, starts immediately, no gate)

**Branch:** `feat/P1-12-web-account-backend`
**Agent:** `db-migration`

| Task | Agent | Notes |
|---|---|---|
| P1-12-BE-3 | db-migration | **One EF migration** adds `Phone` (nullable `varchar`), `Country` (nullable `varchar`), and `AvatarUrl` (nullable `varchar`) to Identity `User` table. All three columns in a **single migration** so BE-1/BE-4/BE-9 can follow without serializing further migrations against each other. Identity schema (`identity` prefix), `UseNpgsql`, `MigrationsHistoryTable` in the identity schema. |

**Serialization rule:** this is the ONLY Identity User migration in this plan. It must be merged to the `feat/P1-12-web-account-backend` branch before any of BE-1/BE-2/BE-4/BE-9 are coded, so they all compile against the updated schema. No other story modifies the Identity `User` table concurrently; if a future story needs a User column change it must branch off after this migration merges.

**Shared-file touches:** `IdentityModuleDbContext` (if columns need explicit config; otherwise EF conventions handle nullable strings), the migration file and snapshot.

**After Batch 1c:** `reviewer` inspects the migration SQL (correct schema, no data loss, reversible). Then dispatch Batch 2.

---

### Batch 2 — P1-12 core features (after Batch 1c migration is reviewed and on branch)

**Branch:** `feat/P1-12-web-account-backend` (same branch, continuing)
**Agent:** `backend-feature`
**Parallelism:** BE-1/BE-8/BE-9 are independent of each other and may run in parallel (separate feature folders, separate controllers). BE-2 follows BE-1.

| Task | Agent | Parallel? | Notes |
|---|---|---|---|
| P1-12-BE-1 | backend-feature | Parallel group A | New `Account/Profile` feature under Identity Application. `GET` profile + `PUT`/update (fullName, phone, country). `[Authorize]` self-scope. `ValidationBehavior` (ICommand). `BaseResponse<T>`. Mirror Catalog CQRS shape. |
| P1-12-BE-8 | backend-feature | Parallel group A | New `Family/UpdateChild` command + handler + validator + controller action beside AddChild/LinkChild. Family-scope authz (`FamilyScopeRequirement`). Returns updated child. Regenerate `api-client` `updateChild` after this task merges. |
| P1-12-BE-9 | backend-feature | Parallel group A | Extend `RegisterParentCommand` + validator to accept `country` (stored from BE-3 column). Add terms-consent record (new entity or field on User) stored at registration. Regenerate api-client after this task merges. |
| P1-12-BE-2 | backend-feature | Sequential after BE-1 | Extend `GetMeQueryHandler` to project fullName/phone/country (+ avatarUrl placeholder) using the new columns. |

**Shared-file touches:** Identity Application `DependencyInjection.cs` — MediatR + validators auto-scan; register only if new typed service is added. `RegisterParentCommandHandler.cs` (BE-9 extends it). Controllers get new actions but are separate files. No `Program.cs` change.

**After Batch 2:** `api-tester` validates the profile, edit-child, and register endpoints. Then `reviewer` gate.

---

### Batch 3 — P1-12 security-sensitive features (starts after Batch 0 approvals for BE-4/BE-5 AND after Batch 1a P1-13a-BE-1 is merged for BE-6)

**Branch:** `feat/P1-12-web-account-backend` (same branch, continuing)
**Agent:** `backend-feature`
**Parallelism:** BE-4/BE-5/BE-6 are independent of each other (different feature folders, different infrastructure concerns) and may run in parallel if three implementers are available. All three depend on the Batch 1c migration (BE-4) or prior auth infra (BE-5/BE-6).

| Task | Agent | Gate | Notes |
|---|---|---|---|
| P1-12-BE-4 | backend-feature | Batch 0 file-storage approval + Batch 1c migration | Avatar upload/remove endpoint. Type + size validation. Safe storage via the approved `IFileStorage` adapter. Set/clear `AvatarUrl`. `[Authorize]` self. Security-sensitive: file upload. |
| P1-12-BE-5 | backend-feature | Batch 0 OAuth approval + provider credentials | OAuth for approved providers. Issues same JWT/refresh as `SignInCommandHandler`. Links/creates parent account. `[Authorize]` on the callback. Security-sensitive: auth + secrets. |
| P1-12-BE-6 | backend-feature | P1-13a-BE-1 merged (email infra) | Password reset request (no enumeration) + set-new-password (token, policy, invalidate other sessions). Calls `IEmailSender` via `SendNotificationCommand` / Notifications integration event. `[Authorize]` on set-new (token-validated). |

**Shared-file touches:** Identity Infrastructure `DependencyInjection.cs` — register `IFileStorage` impl (BE-4) and OAuth provider adapters (BE-5). `appsettings.json` / env for OAuth client IDs/secrets (never committed). No cross-module FK.

**After Batch 3:** run `security-auditor` (P1-12-BE-7 — covers upload + OAuth + reset). Critical/High findings block. Then `api-tester` validates the upload, OAuth, and reset endpoints. Then `reviewer` gate.

---

### Batch 4 — Final reviewer pass + committer

**Agent:** `reviewer` then `committer`

Consolidate all per-batch reviewer notes. The `committer` agent stages and commits each story's branch with a conventional message, pushes, and opens a PR (description includes accepted ACs, test evidence, security-auditor sign-off). Never merges the PR.

Stories ship as separate PRs (one per story branch):
- `feat/P1-13a-notifications-email` → PR for P1-13a
- `feat/P1-13-backend-hardening` → PR for P1-13
- `feat/P1-12-web-account-backend` → PR for P1-12

---

## Review gates

| Gate | After batch | What reviewer checks |
|---|---|---|
| R1 | Batch 1a (P1-13a-BE-1/BE-2) + security-auditor BE-3 | Email delivery works in dev (no-op) and test env; secrets not in appsettings; injection/SSRF findings addressed; `BaseResponse<T>` + `Successed`; module isolation; no cross-module FK |
| R2 | Batch 1b (P1-13-BE-1/2/3) + security-auditor BE-5 | Lockout engaged (unit test); enumeration oracle removed; Admin seed is idempotent + no committed credential; CONVENTIONS compliance |
| R3 | Batch 1c (P1-12-BE-3 migration) | Migration SQL adds nullable columns only (no data loss); identity schema; correct `MigrationsHistoryTable`; reversible |
| R4 | Batch 2 (P1-12-BE-1/2/8/9) + api-tester | Profile GET/PUT returns correct DTO; `/Me` enriched; edit-child enforces family scope; register persists country + consent; all `BaseResponse<T>`; `ValidationBehavior` on commands; `api-client` regenerated |
| R5 | Batch 3 (P1-12-BE-4/5/6) + security-auditor BE-7 + api-tester | Upload: type/size enforced, no executable bypass, storage safe; OAuth: state/nonce, token exchange, account link; reset: no enumeration, token validated, sessions invalidated; all Critical/High findings resolved |
| R6 | Final (all stories) | Cross-story consistency check; HANDOFF.md updated; no `Program.cs` regressions; `dotnet build` green; `dotnet test` green |

---

## Shared-file / serialization rules

| Shared file | Rule |
|---|---|
| Identity `User` table migrations | **Single migration in P1-12-BE-3** adds all three columns (`Phone`, `Country`, `AvatarUrl`). No other task may add an Identity User migration until this one is merged. If P1-13 or another story needs a User column, append to P1-12-BE-3 or create a follow-up migration only after BE-3 is committed. |
| `SignInCommandHandler.cs` | P1-13-BE-1 then P1-13-BE-2 are strictly sequential on this file. Do not attempt to parallelize them. |
| `UserSeeder.cs` | P1-13-BE-3 is independent of BE-1/BE-2 and may be done in parallel by a second implementer. |
| `Notifications.Infrastructure/DependencyInjection.cs` | P1-13a-BE-1 adds `IEmailSender` registration here. Only one implementer may touch this file during Batch 1a. |
| `Identity.Infrastructure/DependencyInjection.cs` | P1-12-BE-4 (IFileStorage) and P1-12-BE-5 (OAuth adapters) both add registrations here. These must be sequential or carefully merged; recommend implementing BE-4 first, then BE-5 on top of the same branch. |
| `Program.cs` | No module-level changes are expected for any of these stories (all three modules are already wired). Touch only if `IEmailSender` or OAuth middleware requires host-level registration — check first before touching. |
| `RegisterParentCommandHandler.cs` | P1-12-BE-9 extends this handler. Only one implementer touches it. It must not conflict with P1-13-BE-2 (which only touches `SignInCommandHandler`). |

---

## Ask-first gates (CLAUDE.md rule #8) — summary

Tasks that ARE blocked and CANNOT start until the lead approves the named design pattern:

| Task | Pattern to approve | Lead decision required |
|---|---|---|
| **P1-13a-BE-1** | `IEmailSender` Adapter (single interface + one real adapter + dev no-op) | (a) Approve the minimal single-adapter design. (b) Choose staging/prod email provider (SMTP relay / SendGrid / SES / other). |
| **P1-12-BE-4** | `IFileStorage` abstraction (dev-local disk adapter, object-store swap later) | Approve the interface + dev-local adapter; confirm no object-store provider credentials are needed yet. |
| **P1-12-BE-5** | OAuth provider adapter per provider (no Strategy selector until warranted) | Approve one-adapter-per-provider design. Confirm which providers to implement now (Google only? Google+Apple?). Confirm credentials available. |
| **P1-13-BE-4** | Pluggable CAPTCHA verifier abstraction (config-gated no-op) | Approve or defer to Phase 6. Gap analysis recommends deferral. |

Tasks that are NOT blocked and can start immediately (no new abstraction):

| Task | Can start | Reason |
|---|---|---|
| P1-13-BE-1 | Now (Batch 1b) | One-line flag change in `SignInCommandHandler` + localized response key |
| P1-13-BE-2 | After P1-13-BE-1 | Collapse error branches in same handler — no new abstraction |
| P1-13-BE-3 | Now (Batch 1b, parallel) | Config-driven seed — no new abstraction |
| P1-12-BE-3 | Now (Batch 1c) | Pure migration — no abstraction involved |
| P1-12-BE-1 | After P1-12-BE-3 | Standard CQRS profile feature — mirrors Catalog |
| P1-12-BE-2 | After P1-12-BE-1 | Extend existing `GetMeQueryHandler` projection |
| P1-12-BE-8 | After P1-12-BE-3 | New command beside existing AddChild — reuses FamilyScopeRequirement |
| P1-12-BE-9 | After P1-12-BE-3 | Extend existing `RegisterParentCommand` — no new abstraction |
| P1-12-BE-6 | After P1-13a-BE-1 approved + built | Reuses Identity password/session services + Notifications IEmailSender; no new abstraction |

---

## Recommended first batch to start now

**While the Batch 0 ask-first decisions are pending, start Batches 1b and 1c in parallel immediately:**

1. **Batch 1b — P1-13 hardening (BE-1/BE-2/BE-3):** These three tasks require zero pattern approvals, touch only existing `SignInCommandHandler.cs` and `UserSeeder.cs`, and close the highest-severity security gaps (lockout, enumeration oracle, hardcoded admin credential). Estimated 8 h total. The lead should dispatch `backend-feature` on `feat/P1-13-backend-hardening` now.

2. **Batch 1c — P1-12-BE-3 migration:** The User schema migration has no approval dependency. Running it now unblocks all of Batch 2 (profile, edit-child, register country/consent). Dispatch `db-migration` on `feat/P1-12-web-account-backend` now.

3. **Batch 1a (P1-13a)** is the critical-path enabler for password reset but requires the email-provider + pattern decision first. Raise the Batch 0 P1-13a decision with the lead in parallel so it can start as soon as possible.

Once Batch 1a + Batch 1c are merged, Batch 2 (P1-12 core) and P1-12-BE-6 (password reset) become unblocked and can run in parallel.

---

## Blockers / open items

| # | Blocker | Blocks | Owner |
|---|---|---|---|
| B1 | **Email provider choice not yet made** (SMTP relay vs SendGrid/SES) — and `IEmailSender` Adapter pattern not yet approved | P1-13a-BE-1 → P1-13a-BE-2 → P1-12-BE-6 (password reset) | Lead must decide before dispatching Batch 1a |
| B2 | **File-storage abstraction not approved** | P1-12-BE-4 (avatar upload) | Lead |
| B3 | **OAuth provider abstraction not approved; provider credentials not confirmed** | P1-12-BE-5 (OAuth) | Lead; also: confirm Google/Apple/Microsoft credentials are available |
| B4 | **CAPTCHA decision** (build now vs Phase 6 deferral) | P1-13-BE-4 | Lead (gap analysis recommends deferral) |
| B5 | **P1-13a reviewer gate** must complete before P1-12-BE-6 (password reset) can start coding — password reset requires a working `IEmailSender` in the Notifications module | P1-12-BE-6 | Sequencing; no external decision needed beyond B1 |
| B6 | **Redis not required for dev** (per HANDOFF.md) but OAuth callback state and token exchange may need a key-value store — confirm whether the existing Redis optional setup is sufficient for OAuth state nonce or a dev-mode in-memory substitute is acceptable | P1-12-BE-5 | Implementer to verify at coding time |

---

## Definition of done

### Per batch

**Batch 1a (P1-13a):** `SendNotificationCommandHandler` no longer throws; dev no-op suppresses sends; staging/prod adapter sends via approved provider; secrets absent from committed files; `UserRegisteredIntegrationEventHandler` sends welcome email best-effort without failing registration; `security-auditor` pass (BE-3) returns no Critical/High; `dotnet build` green; reviewer approves.

**Batch 1b (P1-13 hardening):** After 5 consecutive wrong-password attempts the account locks for 5 min and returns a localized error; sign-in no longer leaks `ex.Message` to callers; user-not-found and wrong-password are indistinguishable to the caller; Admin seed is idempotent, reads from env, and the hardcoded default is removed or guarded in non-Development; `security-auditor` pass (BE-5) returns no Critical/High; `api-tester` validates sign-in error shapes; `dotnet build` green; reviewer approves.

**Batch 1c (P1-12-BE-3):** EF migration adds `Phone`, `Country`, `AvatarUrl` to the identity `User` table; migration is in the identity schema; `dotnet ef database update` applies cleanly; reviewer approves SQL.

**Batch 2 (P1-12 core):** `GET /api/identity/profile` returns fullName/phone/country for the authenticated user; `PUT /api/identity/profile` updates and returns `BaseResponse<T>`; `/Me` includes fullName/phone/country; edit-child command enforces family scope (403 for cross-family attempt); register command persists `country` and terms-consent row; `api-client` regenerated for BE-8/BE-9; `api-tester` pass; `ValidationBehavior` fires on commands; reviewer approves.

**Batch 3 (P1-12 security-sensitive):** Avatar upload rejects non-image/oversized files; stored path does not execute; `AvatarUrl` set on user; OAuth issues same JWT/refresh shape as password sign-in; password reset request returns identical response for registered/unregistered email; reset token validates + invalidates other sessions; `security-auditor` (BE-7) returns no Critical/High; `api-tester` pass; reviewer approves.

### Overall (all stories done)

- All six backend gaps from `docs/briefs/phase-1-backend-gap-analysis.md` are closed (lockout, enumeration, email delivery, welcome message, admin seed; CAPTCHA either closed or formally deferred with a PROGRESS.md note).
- All P1-12 acceptance criteria met: profile, `/Me`, avatar, OAuth, password reset, edit-child, register country + consent.
- `dotnet build backend/Learnexia.Modular.sln` exits 0.
- `dotnet test` exits 0 (unit tests covering at minimum: lockout branch, enumeration-oracle removal, email failure isolation, family-scope authz on edit-child, migration idempotency).
- No cross-module project references introduced; no cross-module FKs.
- `BaseResponse<T>` + `Successed` + `NewResult(...)` used throughout.
- Secrets absent from all committed files.
- `docs/dev/HANDOFF.md` updated with decisions made, any gotchas, and the "what's next" state.
- PRs opened for each story branch; none merged by the agent.

---

Plan ready — dispatch Batch 1.
