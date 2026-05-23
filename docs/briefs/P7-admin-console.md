# Pipeline Brief — Phase 7: Admin Console (P7-01..P7-12)

> **Batch brief** for the whole Admin Console backlog, not a single story. Per-story acceptance criteria + task tables live in the story and task files linked below; this brief carries the **cross-cutting context, handoffs, and open decisions** another lead needs before dispatching the analyzer/planner per story.

## Summary & traceability
- **What:** the admin **feature set** that lives behind the P1-10 dashboard shell. Until this backlog, P1-10 shipped only admin auth + an empty Next.js shell with placeholder nav; there were **no admin feature stories**. This batch fills curriculum management, user/account management, content moderation, and analytics/AI-safety oversight.
- **Stories:** [user-stories/Phase-7-Admin-Console/](../../user-stories/Phase-7-Admin-Console/) — `P7-01..P7-12`.
- **Tasks:** Backend [tasks/Backend/Phase-7-Admin-Console/](../../tasks/Backend/Phase-7-Admin-Console/) · Frontend (Next.js admin app) [tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/](../../tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/).
- **FR-IDs:** new **`FR-ADM-1..12`** group added to [SRS §4.9](../../docs/SRS.md) (prefix is `FR-ADM` — **not** `FR-AD`, which is Adaptivity). §3 Admin role + the goal-traceability matrix were expanded to match. Every P7 story/task traces to an `FR-ADM` id (plus legitimate companion refs FR-CI-3, FR-AI-4, FR-PA-3, NFR-4).
- **BRD goals:** **G5** (scalable platform) primarily; **G2** (learning outcomes / AI safety) for the graph/quiz/moderation/AI-safety stories; **G4** (parent visibility) for user/child-account stories.
- **Phase / Epic:** Phase 7 — Admin Console (post-MVP). Epics: *Admin — Curriculum Management*, *— User & Account Management*, *— Content Moderation & Governance*, *— Analytics & AI Oversight*.
- **Status:** stories + tasks + SRS reqs **merged to `main` via PR #21**. No implementation yet — every P7 row in [tasks/PROGRESS.md](../../tasks/PROGRESS.md) is 🔲 Not started.
- **Built on (already merged):** P1-10 (admin sign-in + shell), P1-05 (RBAC/policies), P2-01 (curriculum hierarchy), P2-11 (skill dependency graph). Some stories also reference P2-06 (quiz model), P3-02 (AI Safety Layer), P5-03 (analytics events), P5-06 (grade transition), P6-02 (AI-safety eval set) — most of which are **not yet built**, so sequencing matters (see below).

## Story index
| ID | Title | Area | SP | Primary FR |
|---|---|---|---|---|
| P7-01 | Manage subjects & units | Curriculum | 5 | FR-ADM-1 |
| P7-02 | Manage lessons & lesson content | Curriculum | 8 | FR-ADM-1 |
| P7-03 | Author skills & the skill dependency graph | Curriculum | 8 | FR-ADM-2 |
| P7-04 | Manage quizzes & questions | Curriculum | 8 | FR-ADM-3 |
| P7-05 | Publish, version & preview curriculum content | Curriculum | 8 | FR-ADM-4 |
| P7-06 | Search & inspect users | User/Account | 5 | FR-ADM-5 |
| P7-07 | Suspend, reactivate & delete accounts | User/Account | 5 | FR-ADM-6 |
| P7-08 | Manage child profiles & grade overrides | User/Account | 5 | FR-ADM-7 |
| P7-09 | Content moderation queue & review actions | Moderation | 8 | FR-ADM-8 |
| P7-10 | Platform analytics & KPI dashboard | Analytics | 5 | FR-ADM-9 |
| P7-11 | AI-safety & quality monitoring dashboard | Analytics | 5 | FR-ADM-10 |
| P7-12 | Admin action audit log | Governance | 5 | FR-ADM-11 |

## Business context & value
- **Who benefits:** internal admins/ops. No student- or parent-facing surface ships here — the surface is the **Next.js `admin-dashboard`** app, gated to the Admin role.
- **Value:** lets the team curate curriculum, manage accounts, moderate AI/uploaded content, and watch platform + AI-safety health **without developer intervention** (today curriculum only exists via seed scripts; there is no admin CRUD). The audit log (P7-12) + admin-only RBAC (FR-ADM-12) make all of this governable.
- **Product decisions in force:** **no teacher role**; **4 subjects** (Math/Science/Arabic/English) — admins curate within them, they don't invent new subjects beyond grade scoping; **parent-driven onboarding** (admins manage accounts, students still don't self-register); **grade transition preserves history** (XP/badges/streaks/mastery) — P7-08 must honor this.

## Cross-cutting architecture & handoffs
- **Frontend (all P7-xx-FE):** land in **`apps/admin-dashboard`** (Next.js 15 + Tamagui via `@tamagui/next-plugin`), reusing `packages/api-client`, `packages/shared`, `packages/ui`, and the **P1-10 admin shell** (`AdminShell`, `useAdminGuard`, authStore). TanStack Query v5 + Zustand v5; RTL/Arabic + English. The shell's placeholder nav items are the entry points these stories fill in.
- **Backend module placement:**
  - Curriculum stories (P7-01..P7-05) extend the existing **`learning`** module (subjects/units/lessons/skills/graph) and **`assessment`** module (quizzes/questions). Quiz↔lesson/skill attach (P7-04) and versioning (P7-05) cross the learning/assessment seam via **`Shared.Contracts`** — **no cross-module FKs**.
  - User/account stories (P7-06..P7-08) extend the **`Identity`** module (parent/child accounts, family link from P1-04). Anything touching gamification/learning history (e.g. P7-08 grade override preserving XP) goes through integration events, not direct joins.
  - Moderation + audit (P7-09, P7-12) are scoped to a **new `Moderation`/`governance` module** (schema-per-module). Content references arrive via `Shared.Contracts` integration events (`AiOutputFlaggedEvent` from P3-02, `CurriculumUploadReceivedEvent` from BL-01); the audit log is fed by an `AdminActionPerformedEvent`.
  - Analytics dashboards (P7-10, P7-11) are **read/aggregate-only** read-models over analytics events (P5-03) and AI-safety signals (P3-02/P6-02) — show **aggregates, not individual child PII** unless drilling into one account.
- **Conventions (apply to every story):** `BaseResponse<T>` via `BaseResponseHandler`, controllers use `NewResult(...)`, success flag spelled **`Successed`**; `ValidationBehavior` only runs for `ICommand<>` (queries paginate but aren't auto-validated); `GenericRepository` commits per call (open an explicit transaction for atomic multi-writes — e.g. reorder/publish/delete-with-children); entities derive from `FullAuditedEntity`; admin endpoints gated with `[Authorize("<Module>.<Action>")]` policies from P1-05; mirror the **Catalog** reference module shapes.

## Open decisions / risks (read before building)
- **D1 — P7-03 skill-graph editor is a design-pattern gate.** The story implies a **visual graph canvas** (drag nodes/edges). It was scoped as a Tamagui-wrapped lightweight graph library with **no new backend abstraction** (it reads/writes the existing P2-11 relational `KnowledgeEdge` graph + reuses its acyclic validator). Per CLAUDE.md rule #8 ("design patterns — ask first"), **confirm the approach with the lead before implementing** rather than introducing a graph/rendering abstraction unilaterally.
- **D2 — Dependency sequencing.** Several P7 stories depend on **not-yet-built** work: P7-04 on the P2-06 quiz/question model; P7-09 on P3-02 (AI Safety Layer) + BL-01 (uploads); P7-10 on P5-03 (analytics events); P7-11 on P3-02 + P6-02 (eval set). The planner should **not** schedule these P7 stories ahead of their upstreams, or should scope a thin slice that degrades gracefully when the upstream signal is absent.
- **D3 — `Moderation`/`governance` module is net-new.** Like any new module it touches the shared serialization points (`Learnexia.Modular.sln`, `Program.cs`, `MediatRExtensions.cs` `AddCrossModuleMediatR`, `Claims.GenerateModules()`). Follow the P2-01 brief's new-module checklist and serialize those edits per [PARALLELISM.md](../../docs/dev/PARALLELISM.md).
- **D4 — Audit-log immutability is the core invariant (P7-12).** Enforce append-only at the DB layer (revoke UPDATE/DELETE / insert-only repository) **and** ensure no command/handler can mutate or delete entries; the handler only reacts to `AdminActionPerformedEvent`. Before/after snapshots must avoid leaking child PII beyond what accountability requires.
- **D5 — Security-sensitive batches.** P7-06/07/08 (child & account data), P7-09 (moderation), P7-12 (audit) are **security-sensitive** — run `security-auditor` before the reviewer gate (access control, child-privacy, data-exposure). Critical/High findings block.
- **D6 — Folder placement.** These live in a dedicated **`Phase-7-Admin-Console`** folder (post-MVP). If the team prefers slotting admin work into existing phases or a different wave, it's a trivial `git mv` + index update — flag before building so task IDs aren't churned mid-implementation.
- **D7 — FR re-verification.** `FR-ADM` ids were authored against the SRS as of PR #21. If the SRS is renumbered, re-verify the `Requirements:` lines across the 12 stories + 24 task files.

## Recommended pipeline order (per story — planner finalizes)
1. Run **analyzer → planner** per story as usual (this batch brief is the shared context, not a substitute for the per-story brief on anything non-trivial).
2. Build **curriculum management first** (P7-01 → P7-02 → P7-03 → P7-04 → P7-05): it's the most self-contained (depends on already-merged P2-01/P2-11) and unblocks demoing real content authoring. **Gate P7-03 on D1.**
3. **User/account management** (P7-06 → P7-07 → P7-08): reuses Identity; route P7-06/07/08 through `security-auditor`.
4. **Moderation + audit** (P7-12 before P7-09 so actions are auditable as they land; or land the `Moderation` module scaffold once for both). Gate on D2 (P3-02/BL-01) and D5.
5. **Analytics dashboards** (P7-10, P7-11): last, since they depend on P5-03 / P3-02 / P6-02 producing data. Read-only — lighter review.
6. Each FE batch consumes a **Design Spec** from the `designer` (these are UI surfaces) before the `frontend` agent builds them.
