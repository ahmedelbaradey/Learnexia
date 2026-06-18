# Execution Plan — P7 Admin Console FRONTEND, Curriculum cluster (P7-01..P7-05)

> Planner: 2026-06-18. **Wave 2 of the admin-dashboard FE** (`apps/admin-dashboard`, Next.js 15, port 3001).
> **Single wave branch `feat/P7-curriculum-fe`** (already checked out) — **NO worktrees** (heavy shared-file coupling across the 5 stories; see §Shared-file serialization).
> **FE-only wave.** Backend (P7-01..P7-05) is fully built + merged in the `Learning` module (wave PR #106). **No `db-migration`, no `backend-feature`, no `api-tester` batches.** Each brief's "live contract" supersedes its stale task file — trust the briefs.
>
> **Path note:** this is the FRONTEND plan. The pre-existing `docs/plans/P7-curriculum-wave.md` is the **backend** wave plan (2026-06-08, PR #106) and is intentionally **not overwritten**. This file mirrors the naming of `P7-admin-users-wave.md` / `P7-user-account-wave.md`.

---

## Source

| Artifact | Path |
|---|---|
| Pipeline briefs (AUTHORITATIVE — correct the task files) | `docs/briefs/P7-0{1,2,3,4,5}-FE.md` |
| FE task files (stale on contracts; used for task IDs/estimates only) | `tasks/Frontend/admin-dashboard/Phase-7-Admin-Console/P7-0{1,2,3,4,5}-FE.md` |
| Backend wave plan (reference, NOT this file) | `docs/plans/P7-curriculum-wave.md` |
| Wave-1 FE foundation (merged) | `apps/admin-dashboard/**`, `packages/api-client/src/{hooks,query,client}/**` |
| Parallelism rules | `docs/dev/PARALLELISM.md` |
| FE architecture | `docs/dev/FRONTEND_ARCHITECTURE.md` |
| Shared memory | `docs/dev/HANDOFF.md` (Wave-1 entry line 41–52; locale limit; admin E2E harness) |
| Conventions | `docs/dev/CONVENTIONS.md` · `CLAUDE.md` |

---

## Wave-level decisions baked into this plan (do NOT re-open mid-wave)

These are derived from the 5 briefs + the prompt constraints and are now LOCKED for all batches. Any change requires re-planning.

1. **FE-only.** No backend/DB/api-tester. If any agent hits a genuine backend gap (e.g. a missing `language` field, a missing block `SetActive` endpoint), **STOP and flag the lead** — do not add backend work on this branch.
2. **Contracts = the briefs, not the task files.** IDs are `int`; mutations return `BaseResponse<string>` (a success message) → **refetch via TanStack invalidation, never read new state from the mutation result**; enums are **ints on the wire** → mirror as `as const` maps (the `ACCOUNT_STATUS` pattern). The task files' `Guid`/`<bool>` returns and stale routes are wrong.
3. **Reuse Wave-1, do not reinvent.** Per-page `<AdminShell title>` self-wrap + `useAdminGuard`; pass-through section `layout.tsx`; `export const dynamic = 'force-dynamic'` on every data page; hand-written admin hooks in `packages/api-client/src/hooks/` mirroring `useSearchUsers`/`useSuspendUser` (raw paths, **NOT** NSwag); primitives `StatusBadge`/`AdminConfirmDialog`/`ReasonField`/`TypedConfirmField`/`AdminErrorBanner`; `lib/strings.ts` EN+AR; the four-state list pattern from `app/(admin)/users/page.tsx`.
4. **CLAUDE rule #8 — ask before patterns/deps.** No new design patterns (compound components, providers, drag abstractions) and **no new npm dependency** without explicit lead approval. This bites two places: the **drag-reorder library** and the **P7-03 graph-viz library** — both default to a no-new-dep approach below; the graph-viz approach is the wave's one true gating decision (§Open decisions D1).
5. **Unified query-key namespace.** The 5 analyzers each proposed a different name (`curriculum.*`, `adminCurriculum.*`, `learningAdmin.*`, `adminLearning.*`, `adminLifecycle.*`). **The whole cluster uses ONE namespace: `adminCurriculum.*`** (see §Unified query-key convention). No story may introduce a second curriculum namespace.
6. **`ADMIN_LOCALE='en'` build constant.** Runtime ar/RTL is unreachable today (HANDOFF). **Author all AR strings anyway** (they're reviewed statically); plan **English-runtime E2E + an ar-strings presence static check**, not runtime RTL E2E. A runtime locale toggle is a separate story.
7. **Lifecycle axis split (load-bearing).** P7-01..04 entities expose only **`IsActive` (bool)** = active/inactive. The **Draft/Published/Archived** axis is owned exclusively by **P7-05** (`ContentLifecycleController`, `entityType`+`entityId`). P7-01's active toggle must stay isolated so P7-05's `CurriculumLifecycleControl` can be composed alongside it later — **do NOT build a Draft→Published→Archived control inside P7-01..04.**

---

## Dependency analysis (why the sub-wave order is what it is)

The cluster is a 3-level content tree plus a cross-cutting lifecycle backbone:

```
Subject (P7-01)  ──owns──▶  Unit (P7-01)  ──owns──▶  Lesson (P7-02)  ──owns──▶  Question (P7-04)
   │                                                      │
   └─ Skill graph (P7-03) relates to a Subject tree       └─ ContentBlocks (P7-02)

P7-05 lifecycle (Draft/Published/Archived + version/rollback/preview/coverage)
     ── cross-cuts ALL of Subject/Unit/Lesson/Question (entityType 1/2/3/4)
```

Hard dependencies (from the briefs):
- **P7-01 is the foundation.** It builds the `/curriculum` route group + layout, the Subjects→Units tree, the **real Curriculum `AdminSideNav` link**, and the first curriculum hooks/query-keys/strings/enum-consts. Everything else mounts under it. (P7-02 Q2 and P7-03 OQ-4 both name P7-01-FE as the navigation/host blocker.)
- **P7-02 (lessons) sits under P7-01's units** — needs the unit context (`unitId`, `subjectId`, inherited `language`) carried down from the P7-01 tree. (P7-02 Q1/Q2/Q3.)
- **P7-04 (questions) sits under P7-02's lessons** — needs a `lessonId` from a lesson list. Without P7-02 it has no in-app entry point. (P7-04 Q2.)
- **P7-03 (skills/graph) relates to a Subject tree** — needs the P7-01 subject picker / `useAdminSubjectList`. Independent of lessons/questions otherwise. (P7-03 OQ-4.)
- **P7-05 is the lifecycle backbone the others reuse.** Its `LifecycleBadge` + `CurriculumLifecycleControl` + lifecycle hooks are *consumed* by P7-01..04 surfaces. Brief P7-05 §"Cross-cutting note" + the prompt say: **sequence the shared lifecycle control EARLY (with or right after P7-01)** so the others can compose it. P7-05's own *hosts* (preview route, coverage landing) don't depend on P7-02/03/04, so P7-05 can ship its shared pieces in parallel with P7-01 once the route group exists.

**Conclusion — three sub-waves, gated:**
- **Sub-wave 2a** = P7-01 (foundation: route group, nav, tree, subject/unit CRUD, shared infra) **+** P7-05 (the cross-cutting lifecycle backbone). These two seed everything and must land first. P7-01's Batch A (shared infra) is the serializer for the whole cluster.
- **Sub-wave 2b** = P7-02 (lessons + content editor) **then** P7-04 (questions under a lesson). Sequential because P7-04's entry point is a P7-02 lesson list. (Internally parallelizable in their foundation batches; see below.)
- **Sub-wave 2c** = P7-03 (skill graph). Depends only on P7-01's subject picker; runs after 2a, can overlap 2b.

---

## Task inventory

> Estimates are the task-file hours (the only sizing available); they predate the contract corrections so treat as rough. Stack is `frontend` (all). Story-level briefs are the spec.

### P7-01 — Subjects & Units (foundation)
| ID | Summary | Est (h) | Depends-on |
|---|---|---|---|
| P7-01-FE-1 | Subjects list page (table keyed by `(subjectCode, language)`; 4 states) | 5 | Wave-1 shell; 2a-A infra |
| P7-01-FE-2 | Language filter/tabs (ar/en/all); scopes grid + disables cross-tree reorder | 3 | FE-1 |
| P7-01-FE-3 | Subject + Unit create/edit forms (pinned-language rule) | 5 | FE-1, 2a-A |
| P7-01-FE-4 | Unit list under subject + drag-reorder (subjects & units, per tree) | 5 | FE-3 |
| P7-01-FE-5 | Per-grade language-coverage panel (6 slots; flag gaps) | 3 | FE-1 |
| P7-01-FE-6 | Activate/deactivate toggle + invalidation (invalidate-only, not optimistic — D8) | 3 | FE-1 |
| P7-01-FE-7 | Soft-delete + "not empty" error surfaced; guard reuse | 2 | FE-1 |

### P7-05 — Publish / version / preview (lifecycle backbone)
| ID | Summary | Est (h) | Depends-on |
|---|---|---|---|
| P7-05-FE-1 | `LifecycleBadge` (Draft/Published/Archived) + `LIFECYCLE_STATE` shared const | 4 | 2a-A infra |
| P7-05-FE-2 | Publish action + confirm; invalidate on success | 4 | FE-1, lifecycle hooks |
| P7-05-FE-3 | Preview view (structured snapshot render, per-tree language) — route `curriculum/preview/[type]/[id]` | 5 | FE-1; D4 |
| P7-05-FE-4 | `VersionHistory` panel + rollback (confirm + required reason) | 4 | FE-2 |
| P7-05-FE-5 | Live-vs-draft state indicator on detail surfaces (RE-SCOPED — D5) | 2 | FE-1 |
| P7-05-FE-6 | `PublicationCoverage` view (ar/en side-by-side; flag one-sided) | 3 | FE-1 |
| — | `CurriculumLifecycleControl` (shared control P7-01..04 reuse) + 4 dialogs | (in FE-2/4) | FE-1 |

### P7-02 — Lessons & content
| ID | Summary | Est (h) | Depends-on |
|---|---|---|---|
| P7-02-FE-1 | Lessons list under a unit + lesson create/edit form | 5 | **P7-01 merged** (unit context); 2b-A |
| P7-02-FE-2 | `ContentBlockEditor` — add blocks by type (text/image/video/callout) | 6 | FE-1 |
| P7-02-FE-3 | Drag-reorder blocks within lesson + reorder lessons within unit | 5 | FE-2 |
| P7-02-FE-4 | Remove block / delete lesson with confirm + invalidation | 3 | FE-1 |
| P7-02-FE-5 | `BlockPreview` inline renderer (**sanitized Markdown** — security) | 3 | FE-2 |
| P7-02-FE-6 | `InheritedLanguageBadge` (read-only, from owning Subject) | 2 | FE-1, P7-01-FE-1 |

### P7-04 — Quizzes & questions (RE-SCOPED to per-lesson question manager — D7)
| ID (orig) | Summary (corrected) | Est (h) | Depends-on |
|---|---|---|---|
| P7-04-FE-1 | ~~Quiz list + QuizForm~~ → **Question manager route + `QuestionList` under a lesson** | 4 | **P7-02 lesson list** (entry point); 2b-A |
| P7-04-FE-2 | `QuestionEditor` — type selector + per-type sub-forms (MCQ/TF/Matching/FillBlank) | 6 | FE-1 |
| P7-04-FE-3 | Per-type client validation + server `Successed=false` inline | 4 | FE-2 |
| P7-04-FE-4 | ~~AttachQuizDialog~~ → **RETIRED** (no Quiz aggregate; questions attach to lesson) | 0 | — |
| P7-04-FE-5 | Reorder / soft-delete / activate questions within a lesson | 3 | FE-2 |

### P7-03 — Skills & dependency graph
| ID | Summary | Est (h) | Depends-on |
|---|---|---|---|
| P7-03-FE-1 | Skills list + create/edit form (name/threshold/time/conceptId) | 5 | **P7-01 merged** (subject picker/hook); 2c-A |
| P7-03-FE-2 | Graph editor scoped to one subject tree — **accessible list/adjacency editor** (D1) | 6 | FE-1 |
| P7-03-FE-3 | Add/remove prerequisite edge (node-id space) — persist via Edges endpoints | 5 | FE-2 |
| P7-03-FE-4 | Cycle + cross-language rejection surfaced inline; no persist | 4 | FE-3 |
| P7-03-FE-5 | Skill detail panel — prerequisites-of / unlocked-by | 3 | FE-1 |

---

## Unified query-key convention (single namespace for the whole cluster)

Add **one** namespace `adminCurriculum` to `packages/api-client/src/query/queryKeys.ts`, mirroring the `adminUsers` object-factory shape. This replaces every per-story name the analyzers proposed. **No second curriculum namespace is permitted.**

```ts
adminCurriculum: {
  all: ['adminCurriculum'] as const,

  // P7-01 — subjects & units
  subjects: (filters?: object) => [...adminCurriculum.all, 'subjects', filters ?? {}] as const,
  subject:  (id: number)       => [...adminCurriculum.all, 'subject', id] as const,
  coverage: (gradeId: number)  => [...adminCurriculum.all, 'coverage', gradeId] as const, // subject-language coverage (P7-01)
  units:    (subjectId: number, filters?: object) => [...adminCurriculum.all, 'units', subjectId, filters ?? {}] as const,
  grades:   ()                 => [...adminCurriculum.all, 'grades'] as const,

  // P7-02 — lessons & content blocks
  lessons:  (unitId: number, filters?: object) => [...adminCurriculum.all, 'lessons', unitId, filters ?? {}] as const,
  lesson:   (lessonId: number) => [...adminCurriculum.all, 'lesson', lessonId] as const, // admin detail incl. blocks
  blocks:   (lessonId: number) => [...adminCurriculum.all, 'blocks', lessonId] as const, // optional if using lesson-detail read

  // P7-04 — questions
  questions: (lessonId: number) => [...adminCurriculum.all, 'questions', lessonId] as const,
  question:  (id: number)       => [...adminCurriculum.all, 'question', id] as const,

  // P7-03 — skills & graph
  skills:        (filters?: object) => [...adminCurriculum.all, 'skills', filters ?? {}] as const,
  skill:         (id: number)       => [...adminCurriculum.all, 'skill', id] as const,
  graph:         (subjectId: number) => [...adminCurriculum.all, 'graph', subjectId] as const,
  prerequisites: (nodeId: number)    => [...adminCurriculum.all, 'prerequisites', nodeId] as const,
  unlockedBy:    (nodeId: number)    => [...adminCurriculum.all, 'unlocked-by', nodeId] as const,

  // P7-05 — lifecycle (entityType + entityId)
  versions:    (entityType: number, entityId: number) => [...adminCurriculum.all, 'versions', entityType, entityId] as const,
  preview:     (entityType: number, entityId: number) => [...adminCurriculum.all, 'preview', entityType, entityId] as const,
  pubCoverage: (gradeId: number)                       => [...adminCurriculum.all, 'pub-coverage', gradeId] as const, // publication coverage (P7-05)
},
```

Rules for the cluster:
- **Distinct from `learning.*`** (student-facing) — different endpoints + DTOs, exactly as Wave-1 kept `adminUsers.*` separate from `users.*`.
- **Note the two coverage keys are different:** `coverage(gradeId)` = P7-01 subject-language coverage; `pubCoverage(gradeId)` = P7-05 publication coverage. Do not merge.
- A P7-05 lifecycle transition on an entity should invalidate the relevant P7-01..04 list (e.g. publishing a Lesson → invalidate `lessons(unitId)` if known + `versions`/`preview`/`pubCoverage`). Hooks pass the affected ids.
- **Shared FE-local DTO/enum types** (`SubjectDto`, `UnitDto`, `LessonDto`, `AdminContentBlockDto`, `AdminQuestionDto`, `SkillGraphDto`, etc.) are hand-written next to their hooks (the `useSearchUsers` convention). The 4 enum const maps (`SUBJECT_CODE`, `CONTENT_LANGUAGE`, `LIFECYCLE_STATE`, plus `DIFFICULTY_LEVEL`/`CONTENT_BLOCK_TYPE`/`QUESTION_TYPE`/`VERSIONED_ENTITY_TYPE`/`GENERATED_BY`) go in `@learnexia/shared/constants` mirroring `ACCOUNT_STATUS`. **2a-A owns the first const block; later batches append disjoint consts.**

---

## Shared-file serialization table (PARALLELISM.md golden rule #3)

Single branch, single working tree → two FE agents editing the same file concurrently **will clobber** (LLM agents do not merge; they overwrite). For each shared file, exactly **one batch owns the edit per dispatch**, and shared-file edits are **dispatched one-at-a-time** (never two agents touching the same file in the same parallel fan-out). Where two batches in the *same* fan-out both need a shared file, they get **strictly disjoint append regions** AND are still dispatched sequentially for that file.

| Shared file | Edit | Owner (first) | Subsequent owners (append-only, disjoint region, sequential) |
|---|---|---|---|
| `apps/admin-dashboard/components/AdminSideNav.tsx` | Promote `curriculum` placeholder → `kind:'real'`, `href:'/curriculum'` (or `/curriculum/subjects`) | **2a-A (P7-01)** — ONE Curriculum nav item only | None. P7-02/03/04/05 do **not** touch nav (they live under `/curriculum/*`; sub-routes are reached from the tree, not new top-level nav items). If a second top-level item is ever wanted, it's a separate serialized edit. |
| `packages/api-client/src/query/queryKeys.ts` | Add `adminCurriculum` namespace | **2a-A (P7-01)** seeds the whole namespace block (all keys above, even ones it doesn't use yet) | Preferred: 2a-A lands the **complete** `adminCurriculum` block up front so 2b/2c/P7-05 only *consume* keys (no further edits). If a key is missed, the needing batch appends inside `adminCurriculum` as a sequential, disjoint edit. |
| `packages/api-client/src/hooks/index.ts` (barrel) | Export new hooks | **2a-A (P7-01)** adds its hook exports under a `// P7-01 …` block | Each later batch appends its own `// P7-0X …` export block at EOF. **Dispatch one batch at a time when it edits the barrel** (never two FE agents writing the barrel in the same fan-out). |
| `apps/admin-dashboard/lib/strings.ts` | Add EN+AR copy slots | **2a-A (P7-01)** adds the `// P7-01 curriculum` block (interface + EN + AR) | Each story gets a **disjoint copy block** with a unique prefix: P7-01 `curriculum*`/`subject*`/`unit*`; P7-02 `lesson*`/`block*`; P7-04 `question*`; P7-03 `skill*`/`skillGraph*`; P7-05 **`clLifecycle*`** (MUST avoid the existing `lifecycle*` P7-07 account namespace). Dispatch one-at-a-time per the barrel rule. |
| `packages/shared/src/constants/index.ts` | Add enum const maps | **2a-A (P7-01)** adds `SUBJECT_CODE` + `CONTENT_LANGUAGE` | P7-05 adds `LIFECYCLE_STATE` + `VERSIONED_ENTITY_TYPE`; P7-02 adds `DIFFICULTY_LEVEL` + `CONTENT_BLOCK_TYPE`; P7-04 adds `QUESTION_TYPE` + `GENERATED_BY` (+ reuse `DIFFICULTY_LEVEL`). Each a disjoint append; sequential dispatch. |
| `apps/admin-dashboard/app/(admin)/curriculum/layout.tsx` | Pass-through section layout (`return <>{children}</>`) | **2a-A (P7-01)** creates it once | Never edited again (it's a pass-through; per-page `AdminShell` does the work). |

> **Within P7-05 specifically:** since P7-05 runs in the **same fan-out as P7-01** (sub-wave 2a), and both touch `strings.ts` / `constants` / the barrel, dispatch them so the **shared-file edits do not overlap in time**: run **2a-A (P7-01 infra, which seeds the namespace/nav/layout/first consts) FIRST and let it land**, THEN dispatch P7-05's foundation batch (2a-C) which only *appends* its `clLifecycle*` strings + `LIFECYCLE_STATE` const + its hook exports. P7-01's feature batch (2a-B) and P7-05's feature batch (2a-D) can then run in parallel because they touch **disjoint component files** and no longer fight over the shared files (those edits already landed in 2a-A / 2a-C).

---

## Execution batches

> Legend: **(seq)** = must follow the prior; **(parallel)** = independent of its sibling in the same fan-out. Every batch is the `frontend` agent unless noted. A `designer` Design Spec precedes each story's UI batches. Gates per sub-wave are in §Review gates.

### SUB-WAVE 2a — P7-01 foundation + P7-05 lifecycle backbone

**Design (before any 2a FE code):**
- **2a-DESIGN-1 — `designer` (P7-01):** Design Spec `design-system/ui_kits/admin-dashboard/P7-01.md` — subjects list layout (grouped table vs tree — recommend grouped-by-grade table per D6), language filter, coverage 6-slot grid, subject/unit forms (pinned-language), reorder affordance + **keyboard path**, all four states, EN/AR copy, RTL. **Resolve D6 (route shape) before this.**
- **2a-DESIGN-2 — `designer` (P7-05):** Design Spec `design-system/ui_kits/admin-dashboard/P7-05.md` — `LifecycleBadge` variants, `CurriculumLifecycleControl` + the 4 confirm dialogs (publish/unpublish/archive/rollback-with-reason), `VersionHistory`, `PublicationCoverage` ar/en grid + one-sided flag, `CurriculumPreview` treatment (per D4). **Resolve D4 (preview render) + D5 (live-vs-draft re-scope) before this.**
- These two specs are independent → run **in parallel**.

**Batch 2a-A (seq, FIRST — the cluster serializer): P7-01 shared infra + route shell.**
`frontend` →
- Create `app/(admin)/curriculum/layout.tsx` (pass-through) + `app/(admin)/curriculum/subjects/page.tsx` skeleton (`force-dynamic`, four-state scaffold).
- Seed the **entire `adminCurriculum` query-key namespace** in `queryKeys.ts` (all keys, incl. ones later stories use).
- Add the P7-01 hooks (`useSubjectList`, `useUnitList`, `useSubjectCoverage`, `useGrades`, create/update/reorder/set-active/delete for subjects + units) + FE-local DTOs; export under a `// P7-01` block in `hooks/index.ts`.
- Add `SUBJECT_CODE` + `CONTENT_LANGUAGE` consts to `@learnexia/shared/constants`.
- Promote the **single** Curriculum `AdminSideNav` item to `kind:'real'`.
- Add the P7-01 `curriculum*/subject*/unit*` strings block (EN+AR).
- **This batch owns every shared file. It must land before anything else in 2a is dispatched.**

**Batch 2a-B (seq after 2a-A): P7-01 feature surfaces.** `frontend` →
- Subjects list + `SubjectLanguageFilter` (FE-1/FE-2), `LanguageCoveragePanel` (FE-5), `SubjectForm`/`UnitForm` (FE-3), unit list + `ReorderableList` (FE-4 — **drag lib gated, D2**), active toggle (FE-6), `CurriculumDeleteDialog` + not-empty error (FE-7). Touches only P7-01 component files + the route pages → no shared-file contention.

**Batch 2a-C (seq after 2a-A, parallel with 2a-B): P7-05 foundation.** `frontend` →
- Add `LIFECYCLE_STATE` + `VERSIONED_ENTITY_TYPE` consts (disjoint append to `constants`).
- Add the 5 lifecycle hooks (`useContentVersionHistory`, `usePreviewContent`, `usePublicationCoverage`, `useTransitionLifecycle`, `useRollbackToVersion`) + FE-local DTOs; append a `// P7-05` export block to the barrel.
- Add `clLifecycle*` strings block (EN+AR — **distinct prefix** from P7-07 `lifecycle*`).
- Build `LifecycleBadge` (FE-1).
- **Shared-file edits here must be dispatched after 2a-A has landed** (so they only append) and **not interleaved with any other barrel/strings/consts writer**.

> **Sequencing inside 2a:** dispatch **2a-A alone first** → on land, dispatch **2a-C alone** (it appends to shared files) → on land, dispatch **2a-B and 2a-D in parallel** (component-only, no shared-file fights). (2a-B may also start right after 2a-A if 2a-C's shared-file edits are confirmed landed; the safe order is A → C → {B ∥ D}.)

**Batch 2a-D (after 2a-A + 2a-C, parallel with 2a-B): P7-05 feature surfaces.** `frontend` →
- `CurriculumLifecycleControl` + the 4 dialogs (`PublishCurriculumDialog`/`UnpublishCurriculumDialog`/`ArchiveCurriculumDialog`/`RollbackVersionDialog` — rollback uses `ReasonField` as a FE-only guardrail, D-OQ2), driven by the legal-transition table; `VersionHistory` (FE-4); `PublicationCoverage` + `/curriculum` landing (FE-6); `CurriculumPreview` + `curriculum/preview/[type]/[id]` route (FE-3, per D4); live-vs-draft state indicator (FE-5, re-scoped per D5). Standalone/composable + a minimal host (D-OQ4) so it's reviewable/E2E-able before P7-01..04 wire it in.

### SUB-WAVE 2b — P7-02 lessons/content, then P7-04 questions

> **Gate:** 2b starts only after **2a (P7-01) is merged** (P7-02 needs the unit-context navigation; P7-02 Q2). P7-05's shared pieces should also be available so the lesson detail can compose `CurriculumLifecycleControl` (entityType=Lesson=3) — but if 2a-D lags, P7-02 ships without the lifecycle control wired and a follow-up wires it (don't block).

**2b-DESIGN — `designer` (P7-02):** Design Spec `design-system/ui_kits/admin-dashboard/P7-02.md` — lessons list + `LessonForm`, the **`ContentBlockEditor`** (block-card list, type picker, 4 per-type forms, add/edit/delete/**keyboard reorder**, saving/error states), `BlockPreview` per type (+ Callout variants + RTL), `InheritedLanguageBadge`, badges, reorder affordance. Resolve D-OQ-P02-route (route shape) with the lead/P7-01 tree.

**Batch 2b-A (seq, after 2a merged): P7-02 foundation.** `frontend` →
- Lesson + content-block hooks (`useLessonsByUnit`, `useAdminLesson`, create/edit/delete/set-active/reorder lessons; add/edit/delete/reorder content blocks) + FE-local DTOs; append `// P7-02` barrel block.
- Add `DIFFICULTY_LEVEL` + `CONTENT_BLOCK_TYPE` consts (disjoint append).
- Add `lesson*/block*` strings (EN+AR, disjoint block).
- Lessons route under the unit (`curriculum/.../lessons`, carries `unitId`/`subjectId`/`language` context).
- **Shared-file edits (barrel/strings/consts) dispatched solo** (no other FE agent writing those files concurrently).

**Batch 2b-B (seq after 2b-A): P7-02 lessons list + form.** `frontend` → `LessonForm` + lessons list (FE-1) + `InheritedLanguageBadge` (FE-6) + four states. Component-only.

**Batch 2b-C (seq after 2b-B): P7-02 content editor + preview.** `frontend` → `ContentBlockEditor` + 4 per-type forms (FE-2) + `BlockPreview` with **sanitized Markdown** (FE-5). **This is the security-sensitive surface.** Component-only.

**Batch 2b-D (seq after 2b-C): P7-02 reorder + delete.** `frontend` → block reorder + lesson reorder (FE-3, **drag lib gated, D2**) + remove-block / delete-lesson dialogs (FE-4). Component-only.

**Batch 2b-E (seq, after P7-02 lesson list exists — i.e. after 2b-B): P7-04 foundation.** `frontend` →
- The 7 question hooks (`useAdminQuestionsByLesson`, `useAdminQuestion`, add/edit/delete/reorder/set-active) + FE-local DTOs; append `// P7-04` barrel block.
- Add `QUESTION_TYPE` + `GENERATED_BY` consts (disjoint append; reuse `DIFFICULTY_LEVEL`).
- Add `question*` strings (EN+AR, disjoint block).
- Question-manager route under a lesson (D-OQ-P04-route).
- **Shared-file edits dispatched solo** — in particular do NOT run 2b-E's barrel/strings/consts edit in the same fan-out as 2b-A/2b-D's. (Simplest: 2b-E starts after 2b-B has landed; the editor batch 2b-C and 2b-D are component-only so they can overlap 2b-E's *component* work but not its shared-file write.)

**Batch 2b-F (seq after 2b-E): P7-04 question editor.** `frontend` → `QuestionList` (FE-1) + `QuestionEditor` type-selector + 4 sub-forms (FE-2) + per-type validation + server-error inline (FE-3) + **the `CorrectAnswer`/`Options` encode-decode rules** (load-bearing; raw scalar for non-Matching, `{pairs:[…]}`/`{left,right}` for Matching). Component-only.

**Batch 2b-G (seq after 2b-F): P7-04 reorder/delete/activate.** `frontend` → reorder + soft-delete (confirm: "hidden from students") + activate/deactivate (FE-5). Component-only.

### SUB-WAVE 2c — P7-03 skill graph

> **Gate:** starts after **2a (P7-01) is merged** (needs the subject picker / `useAdminSubjectList`). Can overlap 2b (disjoint files), but **its shared-file edits (barrel/strings/consts) must be dispatched solo**, not in any fan-out where 2b is also writing those files. **D1 (graph-viz approach) MUST be resolved before 2c-DESIGN.**

**2c-DESIGN — `designer` (P7-03):** Design Spec `design-system/ui_kits/admin-dashboard/P7-03.md` — skills list + `SkillForm`, subject picker, the **accessible list/adjacency graph editor** (per D1), `SkillDetail` panel, cycle/cross-language inline feedback, all states, RTL + a11y (a11y is the hard part — keyboard-operable editor, `aria-live` for edge add/remove + rejection).

**Batch 2c-A (seq, after 2a merged): P7-03 foundation.** `frontend` →
- Skill + graph hooks (`useSkillList`, create/update/delete skill; `useSkillGraph`, `useAddKnowledgeEdge`, `useRemoveKnowledgeEdge`, `useNodePrerequisites`, `useNodeUnlockedBy`; reuse P7-01's subject-list hook — **do not duplicate**, D-OQ4) + FE-local DTOs; append `// P7-03` barrel block.
- Concept picker source (D-OQ3) — consume `GET /api/learning/Concepts/List`.
- Add `skill*/skillGraph*` strings (EN+AR, disjoint block). (Enum consts `NODE_TYPE`/`RELATIONSHIP_TYPE` if needed — disjoint append; reuse `SUBJECT_CODE`/`CONTENT_LANGUAGE`.)
- Skills route shell. **Shared-file edits dispatched solo.**

**Batch 2c-B (seq after 2c-A): P7-03 skills CRUD.** `frontend` → `curriculum/skills/page.tsx` + `SkillForm` (FE-1). Component-only.

**Batch 2c-C (seq after 2c-B): P7-03 graph editor + detail.** `frontend` → `SkillGraph` accessible list/adjacency editor (FE-2) + add/remove edge in node-id space (FE-3) + cycle/cross-language rejection inline (FE-4) + `SkillDetail` (FE-5). Component-only. **(If D1 approves a graph-viz library, that becomes a NEW-DEP sub-step requiring the lead's explicit OK on the package + a `Directory`/lockfile edit serialized like a shared file.)**

---

## Review gates (per sub-wave)

| Sub-wave | `designer` | `security-auditor` | `frontend-e2e-tester` | `reviewer` |
|---|---|---|---|---|
| **2a — P7-01** | required (2a-DESIGN-1) | **SKIP** — curriculum metadata only; no PII/auth/upload/AI/secrets/payments; AdminOnly + automatic P7-12 audit. (Brief P7-01 §Security explicitly says skip.) | required after 2a-B | required (gates 2a-A+2a-B vs P7-01 AC + Design Spec + CONVENTIONS) |
| **2a — P7-05** | required (2a-DESIGN-2) | **REQUIRED** — publishing flips content **live to students**; high-impact, security-sensitive (Brief P7-05 §Risk 7). Audit AdminOnly end-to-end, confirm gates, no-bypass, no snapshot PII leak. **Critical/High block.** | required after 2a-D (publish→state, unpublish, archive, rollback+reason, preview en LTR, coverage one-sided flag, auth routing) | required (gates 2a-C+2a-D vs P7-05 AC; **must include the security-auditor result**) |
| **2b — P7-02** | required (2b-DESIGN) | **REQUIRED** — author **Markdown/content → sanitization sink** (XSS on a children's platform; Brief P7-02 §Security). Audit the Markdown→HTML render path, URL scheme handling, no `dangerouslySetInnerHTML` without sanitization. **Critical/High block.** | required after 2b-D (create/edit lesson, add each block type, edit/reorder/delete block, reorder lessons, soft-delete, validation: bad URL/empty markdown/oversize, inherited-language badge present + no picker, auth) | required (gates 2b-A..2b-D vs P7-02 AC; **must include security result**) |
| **2b — P7-04** | (covered by 2b-DESIGN scope or a P7-04 spec) | **RECOMMENDED, lead's call (default RUN).** No PII, but it's an admin write surface with the **`CorrectAnswer` non-leak** concern (Brief P7-04 §Security flags security-sensitive). Pragmatic default: **run it** (cheap, confirms admin DTO never reaches student paths). If the lead wants to economize, P7-04's only real risk beyond AdminOnly is the `CorrectAnswer` leak — coverable by reviewer + a targeted E2E. **Recommend RUN.** | required after 2b-G (4-type create, **`CorrectAnswer` round-trip read-back**, Matching `{pairs}` shape, per-type invalid-shape server error, edit, soft-delete, reorder, activate/deactivate, auth) | required (gates 2b-E..2b-G vs P7-04 corrected AC) |
| **2c — P7-03** | required (2c-DESIGN) | **SKIP** (default) — curriculum metadata only; no PII/auth/upload/AI/secrets/payments; AdminOnly + automatic P7-12 audit. (Brief P7-03 OQ-5: default not required; reviewer still checks AdminOnly + guard wiring.) Lead may opt in (D-OQ5). | required after 2c-C (skill CRUD, subject select, add edge happy, **cycle rejection**, **cross-language rejection** — needs cross-tree test data, remove edge, prereq/unlock lists, auth) | required (gates 2c-A..2c-C vs P7-03 AC) |

**Cross-cutting gate notes:**
- All E2E runs use the existing **`admin` Playwright project** (`tests/e2e`, `--project=admin`, baseURL `:3001`, SuperAdmin seed `superadmin`/`123Pa$$word!`, `NEXT_PUBLIC_API_URL=http://localhost:5080` in the webServer env, CORS allows `:3001`). **Runtime RTL is BLOCKED** (`ADMIN_LOCALE='en'`) → tests run **English-runtime**; AR coverage is a **static "ar strings present in `lib/strings.ts`" check**, not runtime. Document, don't fail, the RTL cases.
- **Optional `qc-test-designer`** before the tester stages — **recommended for P7-04** (the `CorrectAnswer` round-trip + the loose Matching backend validator make a deliberate, traceable test plan worthwhile) and **for P7-05** (publish/rollback safety). Writes `docs/qc/<StoryID>/`; `frontend-e2e-tester` then implements + reports back.
- **`committer`** per story (or per sub-wave) only after that story's `reviewer` PASSES — all on the single `feat/P7-curriculum-fe` branch (no per-story branches this wave). Include the `docs/dev/HANDOFF.md` update in the PR (what shipped, the `adminCurriculum.*` namespace, the active-vs-lifecycle split, the P7-04 scope correction).

---

## Blockers / prerequisites the lead must clear

1. **GATING DECISION — D1: P7-03 graph-viz approach + any new dependency.** The single decision that changes a story's shape and the only true new-dependency question in the wave. **Resolve before 2c-DESIGN.** Default recommendation: **accessible list/adjacency editor, no new dep** (a11y + RTL + CLAUDE rule #8). A canvas graph-viz library (`reactflow`/`@xyflow`, `cytoscape`, `d3`+`dagre`, …) is a new dep AND a new interaction pattern → needs explicit lead sign-off and, if approved, serialize the lockfile edit. **See §Open decisions D1.**
2. **D2 — drag-reorder library (CLAUDE rule #8).** `ReorderableList` is needed by P7-01 (subjects/units), P7-02 (blocks/lessons), P7-04 (questions). No drag lib exists in the repo. **Default: keyboard move-up/down + buttons (a11y-required anyway), no new dep.** If a drag lib is wanted, lead must approve the package once for the whole cluster (serialize the lockfile edit). Resolve before 2a-B.
3. **D6/D-route — route shapes (P7-01 `[id]` vs inline; P7-02 nested vs `?unitId=`; P7-04 nested vs `?lessonId=`).** Designer-settled but the **P7-01 tree shape gates P7-02/P7-04's entry points** — settle in 2a-DESIGN-1 so 2b can build to it.
4. **Sequencing is itself a prerequisite:** **2b and 2c cannot start until 2a (P7-01) is merged** (navigation/host/subject-picker dependency). **2b-E/2b-F (P7-04) cannot start until 2b-B (P7-02 lesson list) exists** (entry point). This is enforced by the batch order above; the lead must not fan-out 2b/2c before 2a merges.
5. **No hard external blockers** — backend is merged, the admin E2E harness exists, the Wave-1 foundation is in `main`. The wave is unblocked the moment D1/D2/route decisions are made.

---

## Open decisions (consolidated from all 5 briefs — each with a RECOMMENDED DEFAULT so the wave is NOT blocked)

> Most carry a safe default and can proceed **without** a lead round-trip. **Only D1 genuinely needs the lead** (new dependency + interaction pattern). D2 and the scope-corrections (D5/D7) are worth a one-line confirm but have firm defaults.

### Needs the lead (do not silently proceed)
- **D1 (TOP — P7-03 graph visualization + new dependency).** Accessible list/adjacency editor vs canvas graph-viz. **RECOMMENDED DEFAULT: ship the accessible list/adjacency editor now (no new dep);** defer any visual canvas to a follow-up with the library named + signed off. Rationale: a11y/RTL liability of drag-to-connect, and CLAUDE rule #8 forbids a new dep/pattern unilaterally. *Decision changes the 2c shape → confirm before 2c-DESIGN.*
- **D2 (drag-reorder library, cluster-wide).** **RECOMMENDED DEFAULT: keyboard-first reorder (move up/down buttons + `aria-live`), no drag library** for v1; drag is an enhancement. If the lead wants true drag, approve ONE package for the whole cluster and serialize the lockfile edit. *Confirm before 2a-B.*

### Proceed on the default unless the lead objects (one-line confirm at most)
- **D3 (P7-04 quiz-scope correction).** Story/task describe a standalone Quiz aggregate + Attach + language picker that **never shipped**; reality is **implicit per-lesson questions** via `QuestionsController`. **RECOMMENDED DEFAULT: build the per-lesson question manager; RETIRE quiz-CRUD / `AttachQuizDialog` (FE-4) / language picker.** (High confidence — confirmed in code + HANDOFF.) *One-line confirm.*
- **D4 (P7-05 preview rendering).** Snapshot is raw JSON; student renderers are Expo/RN (not usable in Next.js). **RECOMMENDED DEFAULT: structured read-only field preview of the snapshot in the correct direction/locale, labeled "Preview — not published"** — do NOT import the Expo lesson player. *Confirm the visual treatment in 2a-DESIGN-2.*
- **D5 (P7-05 live-vs-draft re-scope).** The task file's `?view=admin` returning `liveVersion`+`draft` **doesn't exist**; backend is per-entity single state. **RECOMMENDED DEFAULT: re-scope FE-5 to "show current `LifecycleState` badge + Preview of current state + last-published via VersionHistory"** — no dual live/draft toggle. *One-line confirm.*
- **D7 (P7-02 media upload — URL-only).** Content-block image/video payloads take a **URL string**; no upload endpoint shipped. **RECOMMENDED DEFAULT: v1 is URL-only (paste an absolute https URL with the SEC-3 host hints); in-dashboard file upload is a later enhancement.** *Confirm if product expects upload now.*
- **D8 (optimistic updates).** Wave-1 used invalidate-and-refetch, no optimism. **RECOMMENDED DEFAULT: invalidate-only for all create/edit/delete/active toggle;** the only place optimism is justified is **reorder** (drag UX) where the batch may optimistically reorder + reconcile/rollback on error. (FE-6 task said "optimistic" — overridden by the Wave-1 precedent + the `string`-message return.) *Proceed.*

### Settle in design (no lead needed)
- **D6 (P7-01 list layout + `[id]` route).** **DEFAULT: grouped-by-grade table** (data is 2 levels, not a deep tree) **+ a dedicated `curriculum/subjects/[id]` route** for the units view (mirrors `users/[id]`, keeps reorder scope unambiguous). Designer confirms in 2a-DESIGN-1.
- **D-route-P02 / D-route-P04.** **DEFAULT: nested context-carrying routes** (`curriculum/subjects/[id]/units/[id]/lessons`, `.../lessons/[id]/questions`) once the P7-01 tree exists; interim `?unitId=`/`?lessonId=` only if a clean nested route is blocked. Designer + the P7-01 tree settle this.
- **D-OQ1 (P7-01 list paging).** **DEFAULT: scope by grade + request a generous `PageSize` (≥12) so all 6 roots show without paging;** keep pagination wiring as a fallback. Coverage uses the dedicated endpoint regardless.
- **D-OQ-lang-filter (P7-01).** **DEFAULT: client-side ar/en/all filter** (no `Language` server param exists); a server param would be a backend change → flag, don't add.
- **D-OQ-confirm-depth (P7-01/03/04).** **DEFAULT: plain confirm dialog for delete (soft + reversible, non-PII); no reason field, no typed-confirm; no confirm on the active toggle.** (Contrast the Wave-1 user-delete two-gate.) Edge-remove inline (no confirm); skill-delete a lightweight confirm.
- **D-OQ-block-active (P7-02).** **DEFAULT: block active state is read-only** (no `SetActive` endpoint for blocks); removal is soft-delete. AC11 block-toggle is out of scope unless BE adds the endpoint — flag, don't build.
- **D-OQ-lang-source (P7-02 inherited language).** **DEFAULT: resolve language FE-side** by carrying the owning Subject's `language` down the tree navigation (route params/props) — no backend change.
- **D-OQ3 (P7-03 concept picker).** **DEFAULT: consume `GET /api/learning/Concepts/List`** (anonymous read is fine from the admin app); scope client-side if no concept→subject filter exists. Flag if scoping is unclear.
- **D-OQ6 (P7-03 Related edges / Strength).** **DEFAULT: hard-code `relationshipType=Prerequisite`, `strength=1.0`** for v1 (story is about prerequisites; cycle guard only runs for Prerequisite). Related/Strength authoring is a later enhancement.
- **D-OQ-P05-reason (rollback reason).** **DEFAULT: required `ReasonField` is a FE-only UX guardrail** (backend does not persist it); the dialog states it's a guardrail. If product wants it persisted, that's a backend follow-up — flag, don't build.
- **D-OQ-P05-publisher (publishedByUserId).** **DEFAULT: show the raw id** (with a label/tooltip); resolve to a name only if a cheap admin user-lookup is approved.
- **D-OQ-P05-host (where the shared control mounts).** **DEFAULT: build the lifecycle control/badge/version-history as standalone composable components + a minimal `/curriculum` host (coverage landing + preview route)** for review/E2E now; P7-01..04 wire them in as they land.

---

## Definition of done

### Per sub-wave
- **2a (P7-01):** All P7-01 ACs (1–10) pass; 6 roots per grade distinguishable by `(subjectCode, language)`; language scope + coverage gaps work; subject/unit CRUD with pinned-language rule; reorder persists within one tree and **cannot cross trees**; activate/deactivate + soft-delete with "not empty" error; Curriculum nav is a real active-aware link; `adminCurriculum` namespace seeded; EN+AR strings present. `reviewer` PASS. E2E (English-runtime) green. **(no security gate.)**
- **2a (P7-05):** `LifecycleBadge` + `CurriculumLifecycleControl` (legal transitions only) + `VersionHistory`+rollback (confirm+reason) + `PublicationCoverage` (one-sided flag) + structured Preview render; `LIFECYCLE_STATE` const; lifecycle hooks invalidate correctly; standalone host exists. **`security-auditor` PASS (no Critical/High).** `reviewer` PASS (includes security result). E2E green.
- **2b (P7-02):** Lessons list + form; `ContentBlockEditor` for all 4 types with add/edit/**reorder**/delete persisting; `BlockPreview` with **sanitized Markdown** (no `dangerouslySetInnerHTML` without sanitization; no `javascript:`/`data:` URL execution); `InheritedLanguageBadge` read-only (no picker); reorder lessons; soft-delete lesson. **`security-auditor` PASS (XSS path closed).** `reviewer` PASS (includes security result). E2E green.
- **2b (P7-04):** Per-lesson `QuestionList` + `QuestionEditor` for all 4 types; **`CorrectAnswer`/`Options` round-trip is symmetric** (non-Matching raw scalar read-back == sent; Matching emits `{pairs:[…]}` + `{left,right}` shapes); per-type client + server validation surfaced inline; reorder/soft-delete ("hidden from students")/activate; **`CorrectAnswer` never reaches any student/shared path.** `security-auditor` PASS (if run). `reviewer` PASS. E2E green incl. the round-trip test.
- **2c (P7-03):** Skills CRUD; subject-picker-scoped graph editor (accessible list/adjacency per D1); add/remove **prerequisite** edge in node-id space; **cycle + cross-language rejection** surfaced inline with no persist; prerequisites-of / unlocked-by lists; keyboard-operable + `aria-live` announcements. `reviewer` PASS. E2E green (incl. cycle + cross-language rejection). **(security gate skipped by default.)**

### Overall (wave)
- All 5 stories' ACs met; **one** `adminCurriculum` query-key namespace; **one** Curriculum nav item; all shared-file edits applied without clobber (disjoint regions, sequential dispatch); enum consts centralized in `@learnexia/shared`; EN+AR strings for all five stories present (ar verified statically); the two mandatory security gates (P7-02, P7-05) PASS with no open Critical/High; all `reviewer` gates PASS; E2E suites green under English-runtime with RTL documented-as-blocked; `docs/dev/HANDOFF.md` updated in the wave PR; everything on `feat/P7-curriculum-fe` (no per-story branches), one PR (or per-sub-wave PRs) opened by `committer`, none merged without the lead.

---

## Dispatch order (one line per step)

1. `designer` 2a-DESIGN-1 (P7-01) ∥ `designer` 2a-DESIGN-2 (P7-05) — after D1/D2/D4/D5/D6 are answered.
2. `frontend` **2a-A** (P7-01 shared infra — the cluster serializer) — **solo**, must land first.
3. `frontend` **2a-C** (P7-05 foundation — appends shared files) — **solo**, after 2a-A lands.
4. `frontend` **2a-B** (P7-01 features) ∥ `frontend` **2a-D** (P7-05 features) — component-only, parallel.
5. `security-auditor` (P7-05) ; `frontend-e2e-tester` (P7-01 then P7-05) ; `reviewer` (P7-01, P7-05) ; `committer`. → **2a merged.**
6. `designer` 2b-DESIGN (P7-02). `frontend` **2b-A** (foundation, solo) → **2b-B** (list/form) → **2b-C** (content editor + sanitized preview) → **2b-D** (reorder/delete).
7. `frontend` **2b-E** (P7-04 foundation, solo, after 2b-B) → **2b-F** (question editor + round-trip) → **2b-G** (reorder/delete/activate).
8. `security-auditor` (P7-02 mandatory; P7-04 recommended) ; `frontend-e2e-tester` (P7-02, P7-04) ; `reviewer` ; `committer`. → **2b merged.**
9. `designer` 2c-DESIGN (P7-03) — after D1 confirmed. `frontend` **2c-A** (foundation, solo) → **2c-B** (skills CRUD) → **2c-C** (graph editor + detail).
10. `frontend-e2e-tester` (P7-03) ; `reviewer` ; `committer`. → **2c merged. Wave done.**

> 2c may overlap 2b in calendar time (disjoint component files) **provided no two FE agents write the same shared file in the same fan-out** — keep all barrel/strings/consts edits serialized across both sub-waves.
