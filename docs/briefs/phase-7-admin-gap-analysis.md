# Phase 7 — Admin Console: Implementation-vs-Spec Gap Analysis

> Written 2026-06-03 by `analyzer`. Read-only audit of the Phase-7 backlog (`P7-01..P7-12`) against `main`. The whole P7 set is **post-MVP** and was expected to be 🔲 not started — this brief verifies that and surfaces the **spec drift + incidental code drift** that has accumulated since the P7 backlog was authored in PR #21 (notably: Catalog module removed, P4-* gamification stories shipping, MinIO storage relocated to `Shared.Kernel`).

## 1. Summary & method

**What was compared.** All 12 P7 user stories, all 12 P7 BE task files, all 12 P7 FE task files, the batch brief `docs/briefs/P7-admin-console.md`, the SRS `FR-ADM-1..12` block (§4.9 + §3 + goal matrix), `tasks/PROGRESS.md`, `docs/dev/HANDOFF.md`, the `info/` corpus, `docs/architecture.md`, and the actual code on `main`:
- `apps/admin-dashboard/**` — the Next.js admin shell from P1-10.
- `backend/src/Modules/**` — Identity / Learning / Gamification / Notifications / Parent (the only 5 modules; Catalog removed 2026-06-03; no `assessment`, no `Moderation`, no `Governance`).
- `backend/src/Shared/Learnexia.Shared.Kernel/**` — `AuthorizationPolicies.AdminOnly`, `IStorageService`/MinIO.

**Framing.** Phase 7 is post-MVP. Expected state: zero P7 code on `main`, plus a P1-10 admin shell. The audit therefore focuses on (a) any **incidental code drift** that already exposes P7 surface area, (b) **prerequisite-story status** (P2-06, P3-02, P5-03, P5-06, P6-02, BL-01), and (c) **stale references** in the P7 stories/tasks that would mislead an analyzer dispatched against them today.

**One-line headline.** All 12 P7 stories are 🔲 not started in both BE and FE (confirmed against `PROGRESS.md` and the source tree). The shell exists, but every P7 prerequisite outside Phase 1–2 is missing. The biggest spec issues are **(i)** every BE task file says "mirror Catalog" — Catalog no longer exists; **(ii)** P7-04 + P7-05 BE tasks place quizzes/versions in an `assessment` module that **was never built** (P2-06 quiz/question entities actually live inside `learning`); **(iii)** the **P2-01 admin CRUD endpoints that already exist are completely unauthenticated** — incidental drift that P7-01..P7-04 silently inherit.

## 2. Implementation status table (per story)

Legend: 🔲 Not started · 🟡 Partial · ✅ Built. "Prereqs" lists prerequisite stories beyond Phase 1 + P1-10 + P1-05; "—" = none beyond those.

| ID | Title | BE | FE | Prereqs status | Spec gaps? |
|---|---|:--:|:--:|---|---|
| P7-01 | Manage subjects & units | 🔲 | 🔲 | **P2-01 ✅** — `Subject`/`Unit` entities + Create/Update/Delete endpoints already exist, but with no `[Authorize]`, no `IsActive` on `Subject` or `Unit`, no `SequenceOrder` on `Subject`, no Reorder endpoint, and no "unit not empty" guard. P7-01 must both **add policy gates to the existing endpoints** and add the missing fields/operations — the task file doesn't call that out. | Yes — "mirror Catalog" stale; existing unauthenticated CRUD must be hardened, not freshly built. |
| P7-02 | Manage lessons & lesson content | 🔲 | 🔲 | **P2-01 ✅** (Lesson exists with `LessonsController` Create/Update/Delete, unauthenticated). `ContentBlock` entity does **not** exist. | Yes — "mirror Catalog" stale; same auth gap as P7-01 on existing Lesson CRUD; payload type set (`text/image/video/callout`) was never validated against the design-system Lesson Renderer story (P2-05) — risk that block types diverge. |
| P7-03 | Author skills & graph editor | 🔲 | 🔲 | **P2-01 ✅** (Skill + `SkillsController` Create/Update/Delete, unauthenticated), **P2-11 ✅** (`KnowledgeNode`/`KnowledgeEdge` + acyclic validator + prereq/unlock queries). Edge-author commands (AddEdge/RemoveEdge) and `GetGraph` query don't exist. | Yes — "mirror Catalog" stale; **D1 in the batch brief (visual graph canvas) is unresolved** — Rule #8 (ask-first design pattern) gate still open. |
| P7-04 | Manage quizzes & questions | 🔲 | 🔲 | **P2-06 partially ✅ but in the WRONG module.** `QuizQuestion` + `QuestionType` enum + per-type validators (`QuizQuestionTypeValidation.cs`) + attempt endpoints (`QuizzesController`) live inside **`Modules/Learning`**, not in a separate `assessment` module. There is **no `assessment` module** anywhere in `backend/src/Modules/`. There are no Quiz CRUD admin endpoints. | **Yes — blocker.** The BE task says the stack is "`assessment` module" and the FE task hits `/api/Assessment/...`; both must be rewritten to target `learning` (or the team must explicitly decide to scaffold a new `assessment` module — would be a new-module decision per CLAUDE.md memory "Ask before new modules" and the HANDOFF "quiz/assessment lives in the Learning module"). |
| P7-05 | Publish, version & preview | 🔲 | 🔲 | **Depends on P7-01..P7-04.** No `LifecycleState` / `ContentVersion` entity exists today. | Yes — spans `learning + assessment`; same `assessment` issue as P7-04. "Mirror Catalog" stale. |
| P7-06 | Search & inspect users | 🔲 | 🔲 | **Identity ✅** (P1-01/03/04 family link present in **Parent** module). No `SearchUsersQuery` / `AdminUsersController` exists. Activity-summary cross-module seam doesn't exist either. | Minor — the FR is OK, but the BE task lists `[Authorize(Identity admin policy)]` without naming the existing `AuthorizationPolicies.AdminOnly` constant — use that. |
| P7-07 | Suspend, reactivate, delete accounts | 🔲 | 🔲 | **Identity ✅** but `ApplicationUser` has no `AccountStatus` field today; lockout exists (P1-13) but is distinct. Integration events `AccountSuspended/Reactivated/Deleted` do not exist in `Shared.Contracts`. | Minor — same policy-naming nit as P7-06. |
| P7-08 | Manage child profiles & grade overrides | 🔲 | 🔲 | **Identity ✅** for child profile fields; **P5-06 🔲** (grade transition) — the `ChildGradeChanged` integration event referenced is not yet defined. P7-08 must either ship the event itself or block on P5-06. | Yes — dependency on `ChildGradeChanged` contract not yet present; current story says "same contract used by P5-06" which doesn't exist. |
| P7-09 | Content moderation queue | 🔲 | 🔲 | **P3-02 🔲** (AI Safety Layer) + **BL-01 🔲** (curriculum upload) — both upstreams missing. `AiOutputFlaggedEvent` / `CurriculumUploadReceivedEvent` are not in `Shared.Contracts`. **No `Moderation` module exists.** | **Yes — blocker for now.** "New `Moderation` module mirrors Catalog" — Catalog gone, and a new module requires scaffolding (Program.cs, sln, MediatRExtensions, Claims.GenerateModules, schema) before P7-09 backlog can begin. Either upstream P3-02/BL-01 events ship first or P7-09 carries a degraded slice. `IStorageService` now in `Shared.Kernel` — if moderation items reference uploaded blobs, P7-09 should use that (not module-local MinIO). |
| P7-10 | Platform analytics & KPI dashboard | 🔲 | 🔲 | **P5-03 🔲** (analytics event capture) — there is no event store / analytics read source on `main`. | Yes — dependency missing; story is read-only over data that doesn't exist yet. Without P5-03 there's nothing real to query. |
| P7-11 | AI-safety & quality monitoring dashboard | 🔲 | 🔲 | **P3-02 🔲** + **P6-02 🔲** + AI Gateway (P3-01 🔲) — none built. | Yes — same situation as P7-10. Dashboard reads 3 signals, none of which exist on `main`. |
| P7-12 | Admin action audit log | 🔲 | 🔲 | None hard-blockers; but the entity is placed in the "**Moderation/Governance** module schema" which doesn't exist. | Yes — module-placement decision: ship `AuditLog` in a new `Moderation` (or split `Governance`) module, or alternatively in `Identity` since most audited actions are Identity ones; needs an explicit decision. P7-12 should likely land **before** P7-06..P7-09 (those all reference its `AdminActionPerformedEvent`). |

**Counts.** 12/12 stories Not started in both BE and FE; **0 Built**, **0 Partial**, **12 Not started**.

## 3. Cross-cutting code-state findings

### 3.1 `apps/admin-dashboard` shell — exactly what was shipped by P1-10
- Files present: `app/(admin)/layout.tsx`, `app/(admin)/dashboard/page.tsx` + `DashboardLanding.tsx`, `app/login/page.tsx`, `app/providers.tsx`, `middleware.ts`, plus `components/AdminShell.tsx`, `AdminSideNav.tsx`, `AdminTopBar.tsx`, `AdminErrorBanner.tsx`, `AdminLoadingSkeleton.tsx`, plus `lib/{apiClient.ts,jwt.ts,signInSchema.ts,strings.ts}` and `lib/hooks/{useAdminGuard.ts,useSignOut.ts}`, plus `tamagui.config.ts` + `next.config.ts`.
- `AdminSideNav.tsx` renders **two placeholder, non-functional nav items only**: `curriculum` and `content` (both `aria-disabled`, no-op click, no routes mounted). Nothing for users/moderation/analytics/audit.
- `DashboardLanding.tsx` is a single placeholder card.
- **No P7 feature is wired beyond the shell.** Confirmed.

### 3.2 Missing modules / entities required by P7 BE
- **No `assessment` module.** `backend/src/Modules/` contains exactly: `Identity`, `Learning`, `Gamification`, `Notifications`, `Parent`. The P2-06 quiz/question/answer model lives inside `Modules/Learning` (`Domain/Entities/QuizQuestion.cs`, `Domain/Enums/QuestionType.cs`, `Application/Features/Questions/Validation/QuizQuestionTypeValidation.cs`, `Api/Controllers/QuizzesController.cs`). **P7-04-BE.md + P7-05-BE.md + the P7-09 task referring to "`learning` + `assessment` modules" all misstate the module placement.**
- **No `Moderation` / `Governance` module.** P7-09 + P7-12 both need a new module. Per the MEMORY entry "Ask before new modules" and CLAUDE.md rule #8, this requires explicit lead approval before scaffolding (also serializes the shared-file edits per `PARALLELISM.md`: `Program.cs`, `Learnexia.Modular.sln`, `MediatRExtensions.AddCrossModuleMediatR`, `Claims.GenerateModules()`).
- **No `assessment` module needed if quiz/question stays in `Learning`** (HANDOFF + MEMORY say this is the canonical placement). Recommended: keep quiz in `Learning`, rewrite P7-04/P7-05 BE tasks accordingly. Net: **one** new module (`Moderation`) for the whole batch, not two.

### 3.3 P2-01 admin CRUD already exists — and is unauthenticated
This is the **most impactful incidental drift** for the P7 backlog:
- `SubjectsController.Create/Update/Delete`, `UnitsController.Create/Update/Delete`, `LessonsController.Create/Update/Delete`, `ConceptsController.Create/Update/Delete`, `GradesController.Create/Update/Delete`, `SkillsController.Create/Update/Delete` all exist with **no `[Authorize]` attribute at all** (verified by grep; only the two student-facing reads `GetLessons` / `GetSkillTree` are `[Authorize]`-gated for "any authenticated user").
- Domain entities lack the P7-01 contract: `Subject` has no `SequenceOrder` and no `IsActive`; `Unit` has `SequenceOrder` but no `IsActive`; `Lesson` likewise; there are no Reorder/Activate handlers.
- **Implication for P7-01..P7-03:** these stories are not greenfield "build CRUD"; they are "harden + extend existing CRUD with policy + soft-state + reorder + per-type validators". The task files don't reflect this — they read as if the controllers don't yet exist. An analyzer dispatched today would scaffold duplicates and miss the auth hole.
- Note: this is also arguably a **Phase 1 / Phase 2 security debt** (the endpoints exist on `main` open to anyone) — worth flagging to the lead separately as a P6-06 candidate or a follow-up to P1-05.

### 3.4 Catalog removal vs P7 task references
- HANDOFF top section confirms Catalog deleted 2026-06-03 (PR #84). `backend/src/Modules/Catalog` no longer exists in the source tree; the only residual "Catalog" string matches in `Modules/Learning` are docstring/comments about the prior reference shape (LearningRepository.cs:18, LearningDbContext.cs:11/18/55).
- **5 of 12 P7 BE task files still say "Mirror Catalog"** (P7-01, P7-02, P7-03, P7-04, P7-05; plus the P7-09 phrase "New `Moderation` module mirrors Catalog"). HANDOFF says the new reference is "an existing module (e.g. **Learning**)" — every "mirror Catalog" line should become "mirror **Learning**" (or "mirror an existing module").
- `docs/architecture.md` is **also stale** — §1, §4.2, the Mermaid graph at line ~37, and `AddCatalogModule` at line 78 all still reference Catalog. This is broader than P7 (a planner/designer dispatched anywhere will hit it) but it directly affects every P7 task's interpretation of "mirror Catalog".

### 3.5 FR-ADM trace validity
- Verified the SRS §4.9 `FR-ADM-1..12` block (lines 115–128) and §3.5 Goal Matrix (lines 389–392). The 12 `FR-ADM` ids are present, correctly **`FR-ADM` not `FR-AD`** (which is Adaptivity), and each story references its primary FR. The companion references (FR-CI-3, FR-AI-4, FR-PA-3, NFR-4) used in P7-03/09/10/11/12 are also valid. **No FR-renumber needed.**
- Decision D7 in the batch brief (re-verify FR-ADM if SRS renumbers) can be marked **resolved-no-action** as of today.

### 3.6 `IStorageService` now in `Shared.Kernel`
- Per HANDOFF P1-12 BE-4: MinIO storage is registered **once at Host** as a platform-wide capability (`AddMinIODependencies`). `IStorageService` lives in `Shared.Kernel.Abstractions.Storage`, the impl lives in `Shared.Kernel.Storage.StorageService`. Modules consume it directly; no module-local registration.
- **P7-09 implication:** any moderation item that references an uploaded artifact (curriculum upload from BL-01, or admin-attached evidence on a moderation decision) must inject `IStorageService` from `Shared.Kernel` — **not** scaffold its own MinIO wiring. Same for P7-02 if lesson content blocks ever carry uploaded media; same for P7-12 if exports stage to object storage.
- The current P7-09-BE.md / P7-02-BE.md / P7-12-BE.md don't mention `IStorageService` — should be added as a note.

### 3.7 P1-05 policy readiness for P7
- `Shared.Kernel/Abstractions/AuthorizationPolicies.cs` defines exactly **one** named policy constant: `AdminOnly`. Already enforced in `TimedEventsController` (P4-11) and notifications/admin user-management endpoints.
- The P7 BE tasks reference five different policy names — `Learning.ManageCurriculum`, `Assessment.ManageQuizzes`, `Moderation.Review`, `Audit.Read`, "Identity admin policy", "Analytics admin policy". **None of these exist.** Options: (a) extend `AuthorizationPolicies` with per-area constants and register them in Identity's `AddAuthorization` (preferred — keeps the shared-source-of-truth pattern), or (b) collapse to the existing `AdminOnly` for the whole batch (simpler, matches current per-FR design of `FR-ADM-12 admin-only`). The batch brief was silent on this; needs a lead decision.

## 4. Spec gaps & stale references — concrete edits needed

Listed in priority order; each is a small surgical edit to the linked file.

1. **`docs/architecture.md` Catalog references → Learning (broader than P7 but blocks P7 analyzers).** Update §1, §4.2, the Mermaid module map, and the `Program.cs` snippet so the reference module is `Learning`. Open a separate housekeeping PR if not folded into the first P7 PR. (Severity: **Important** — every P7 task file derefs this.)
2. **All "mirror Catalog" lines in P7-01..P7-05 BE task files → "mirror an existing module (e.g. Learning)".** Files: `tasks/Backend/Phase-7-Admin-Console/P7-01-BE.md:32`, `P7-02-BE.md:32`, `P7-03-BE.md:32`, `P7-04-BE.md:32`, `P7-05-BE.md:33`, `P7-09-BE.md:33`. (Severity: **Minor** but mechanical.)
3. **P7-04-BE.md + P7-05-BE.md — module placement.** Replace every "`assessment` module" with "`learning` module" (or open a deliberate "do we scaffold a new `assessment` module?" decision — see §6 Q1). Also update the P7-04-FE.md `/api/Assessment/...` paths to `/api/Learning/Quizzes/...`. The story files **P7-04** + **P7-05** also say "`assessment` module" — same edit there. (Severity: **Blocker** — pipeline would fail otherwise.)
4. **P7-01..P7-03 BE tasks — acknowledge existing unauthenticated CRUD.** Each story should explicitly add a task like *"BE-0: Add `[Authorize(AdminOnly)]` to the pre-existing `SubjectsController`/`UnitsController`/`LessonsController`/`ConceptsController`/`SkillsController` Create/Update/Delete endpoints (P2-01 left them open); add migrations for missing `SequenceOrder` / `IsActive` columns on `Subject`/`Unit`/`Lesson`."* The current tasks read as if those controllers don't exist. (Severity: **Important** — also flags a live auth hole on `main`.)
5. **P7-09 + P7-12 — `Moderation` module scaffolding task is implicit.** Add an explicit pre-task **"P7-09-BE-0: scaffold the `Moderation` module (4 csproj — Domain/Application/Infrastructure/Api — sln entry, `Program.cs` wiring, `MediatRExtensions.AddCrossModuleMediatR` entry, `Claims.GenerateModules()` entry, schema migration scaffold)"** — same shape as the P2-01 brief had for `learning` and the P4 stories had for `gamification`. Per CLAUDE.md rule + MEMORY "Ask before new modules", this needs a lead approval gate before scaffolding (see §6 Q2). The P7-12 audit log should land **inside** that module from day one. (Severity: **Important**.)
6. **P7-12 — placement and ordering.** Currently the dependency order in the batch brief lists "P7-12 before P7-09 so actions are auditable as they land". Lock that in the per-story plans: **P7-12-BE-1..3 must ship before P7-06/07/09 emit `AdminActionPerformedEvent`.** Today P7-06/07 task files reference P7-12 with "P7-12-BE-*" as a dep — that ordering needs to be enforced by the planner. (Severity: **Important** — sequencing only.)
7. **P7-08 — `ChildGradeChanged` contract dep on P5-06.** P5-06 is 🔲 not started. Either (a) P7-08 ships the `Shared.Contracts.Identity.ChildGradeChanged` integration event itself (and P5-06 later reuses it), or (b) P7-08 blocks on P5-06. The task file currently assumes P5-06 owns the contract — needs a tie-break. (Severity: **Important**.)
8. **`IStorageService` note in P7-02 / P7-09 / P7-12.** Add a one-line "uploaded artifacts use `Shared.Kernel.Abstractions.Storage.IStorageService` (registered once at Host per P1-12 BE-4) — do not register MinIO at the module level". (Severity: **Minor**.)
9. **Policy names.** Either extend `AuthorizationPolicies` (Curriculum/Quizzes/Moderation/Audit/Analytics) and call out the constants in each BE task file, or replace every per-area policy name in P7 BE tasks with `AuthorizationPolicies.AdminOnly`. (Severity: **Important** — see §6 Q3.)
10. **Batch brief D6 / D7 — close them.** D7 (FR re-verification) is now **resolved-no-action** per §3.5. D6 (folder placement) — `Phase-7-Admin-Console` folder is intact + indexed; if the team is OK keeping it post-MVP, also mark **closed**.

## 5. Recommended path forward

The batch brief's order (curriculum → user/account → moderation+audit → analytics) is still correct; this brief refines it with what's safe to start **today** vs **blocked by upstream phases**.

**A. Safe to start now** (only need P1-05 + P1-10 + P2-01 + P2-11 — all merged):
1. **Curriculum-mgmt wave** — *but only after* fixes #1, #2, #3, #4 above are applied and one decision (§6 Q1 — keep quiz in `learning`).
   - **P7-01 → P7-02 → P7-03 → P7-04 → P7-05.** Each story now has a clear pre-existing-CRUD-hardening task at the front (fix #4). P7-03 still gated on D1 (visual graph editor — Rule #8 ask-first; see §6 Q4).
   - One PR per story per the standard cadence; designer batch before each FE.
2. **User/account-mgmt wave** — needs only Identity + Parent (both ✅).
   - **P7-06 → P7-07 → P7-08** (with P7-08 carrying its own `ChildGradeChanged` contract per fix #7 if P5-06 hasn't landed). Route P7-06/07/08 through `security-auditor` per CLAUDE.md §2 step 4b.
3. **Audit log: P7-12.** Standalone — depends only on the new `Moderation` module scaffold. Land **before** P7-06/07/08/09 emit audit events. This means P7-12 actually wants to move to the **front** of the user/account wave, despite its number.

**B. Blocked / not safe to start** (upstream phases not built):
4. **P7-09 (Moderation queue)** — needs P3-02 (AI Safety Layer) + BL-01 (curriculum upload) integration events. Possible thin slice: ship `Moderation` module + queue UI fed by a manual "admin reports content" command, and wire the AI/upload event consumers later when those upstreams land.
5. **P7-10 (Platform analytics)** — needs P5-03 (analytics events). No way around this; defer.
6. **P7-11 (AI safety dashboard)** — needs P3-02 + P6-02 + an AI Gateway (P3-01). All three are 🔲. Defer.

**Recommended ordered backlog (lead's queue):**
0. Apply spec edits #1–#10 (a single docs PR).
1. **P7-01** (start of curriculum wave) — also closes the unauthenticated-CRUD hole.
2. **P7-02** (lessons + content blocks).
3. **P7-03** (skills + graph editor) — gate on Q4 (visual canvas approach).
4. **P7-04** (quizzes) — in `learning`, not `assessment`.
5. **P7-05** (versioning/publish/preview).
6. **P7-12 + Moderation module scaffold** (audit log first so P7-06/07/08 can emit events).
7. **P7-06 → P7-07 → P7-08** (user/account wave, `security-auditor` gated).
8. Wait on P3-01/P3-02/P5-03/P6-02/BL-01, then **P7-09 → P7-10 → P7-11**.

## 6. Open questions / decisions for the lead

Each is a decision a human needs to make before the analyzer can finalize per-story briefs.

- **Q1 — Quiz module placement.** Stay with quiz/question in `Modules/Learning` (matches `main` + HANDOFF + MEMORY "quiz/assessment lives in Learning") and rewrite P7-04/05 BE+FE tasks, or scaffold a new `assessment` module (would also need to physically move P2-06 entities + migrations and update DTO namespaces — much larger). **Recommendation: stay in `Learning`.**
- **Q2 — Moderation module scaffold.** Confirm the `Moderation` module (the home for `ModerationItem` + `AuditLog`) is OK to scaffold, and confirm it's one module covering both moderation queue (P7-09) and the audit log (P7-12), not split into `Moderation` + `Governance`. Per CLAUDE.md rule + MEMORY "Ask before new modules", explicit approval needed. **Recommendation: one `Moderation` module owning both.**
- **Q3 — Policy granularity.** Add `AuthorizationPolicies.{ManageCurriculum, ManageQuizzes, ReviewModeration, ReadAudit, ReadAnalytics}` and register them in `Identity.Infrastructure.AddAuthorization` against the `Admin` role, **or** collapse all P7 to the existing `AdminOnly` policy for now and split later if/when a junior-admin role is introduced. **Recommendation: collapse to `AdminOnly` for P7 MVP; split when a real role exists** (FR-ADM-12 is admin-only either way).
- **Q4 — P7-03 graph editor (Decision D1 in the batch brief, still open).** Visual canvas via a Tamagui-wrapped lightweight graph library, reading/writing the existing relational P2-11 graph + reusing the acyclic validator, with **no new backend abstraction**. CLAUDE.md rule #8 still requires explicit approval before introducing the FE graph library / pattern.
- **Q5 — Unauthenticated P2-01 CRUD on `main`.** Should the auth-gate fix be folded into P7-01 (alongside the new admin features) or hot-fixed in a separate small PR first? It is currently a live hole on `main` and a candidate to bundle into the P6-06 hardening pass. **Recommendation: hotfix PR first** (it's a 6-line change to add `[Authorize(AdminOnly)]` on existing controllers); P7-01 then extends with reorder/activate.
- **Q6 — `ChildGradeChanged` contract owner.** P7-08 or P5-06? If P5-06 won't land before P7-08, P7-08 owns it. **Recommendation: ship the contract in P7-08; P5-06 reuses.**
- **Q7 — Post-MVP timing.** P4-* gamification is still in flight (P4-08 FE WIP, P4-11 BE pending PR per HANDOFF top). Is it realistic to start the curriculum-mgmt wave concurrently, or should P7 wait until Phase 4 frontend closes? Per `PARALLELISM.md` independent siblings only — P7-01/02 don't touch gamification code, so concurrency is technically OK, but lead bandwidth for review may be the real bottleneck.
- **Q8 — P7-09 thin slice.** If P3-02 + BL-01 won't land for a while, do we ship a P7-09 thin slice fed only by a manual "admin reports content" command (decoupled from AI/upload events), or defer P7-09 entirely until those upstreams exist?

---

**Cross-references:**
- Batch brief: `docs/briefs/P7-admin-console.md`
- SRS: `docs/SRS.md` §4.9 (lines 115–128), §3, §3.5 (lines 389–392)
- HANDOFF: `docs/dev/HANDOFF.md` (Catalog-removal section; P1-12 BE-4 storage relocation)
- PROGRESS: `tasks/PROGRESS.md` (Phase 7 table, lines ~108–122)
- Admin shell: `apps/admin-dashboard/components/AdminSideNav.tsx`, `app/(admin)/dashboard/DashboardLanding.tsx`
- Shared.Kernel auth policy: `backend/src/Shared/Learnexia.Shared.Kernel/Abstractions/AuthorizationPolicies.cs`
- Existing P2-01 unauthenticated CRUD: `backend/src/Modules/Learning/Learnexia.Modules.Learning.Api/Controllers/{SubjectsController,UnitsController,LessonsController,ConceptsController,SkillsController,GradesController}.cs`
- Stale architecture doc: `docs/architecture.md` (§1, §4.2, Mermaid module map, `AddCatalogModule` snippet)
