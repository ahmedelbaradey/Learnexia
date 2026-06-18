# Execution Plan — Phase-5 Parent-Scoped READ API wave (backend-only)

> **Scope:** the FULL parent-read slice across **P5-08** (live reads E1–E4, E6, E7, E8), **P5-02** (weak-area detection — E5), and **P5-01** (scheduled weekly report + `WeeklyReport` table — E9).
> **Backend-only build** (this lead). FE is the other lead: `P5-05-FE` swaps `parentDashboardStubs.ts` 1:1 against these endpoints — author nothing FE; the contract is noted under "FE contract (for awareness)".
> **Lead decisions locked 2026-06-18 (do not re-litigate):** new story **P5-08** for the live reads; read controller + fan-out handlers live in the **Parent** module (NO new module); FULL slice including E5/P5-02 and E9/P5-01.

## Source
- **Brief:** `e:\Wrokspace\Learnexia\docs\briefs\P5-parent-read-api.md` (9 endpoints E1–E9, data-availability table, seam inventory, reference handler).
- **Stories:** `user-stories/Phase-5-Parent-Analytics/P5-08-parent-scoped-read-api.md`, `…/P5-02-weak-area-detection.md`, `…/P5-01-weekly-report-generator.md`.
- **Task files:** `tasks/Backend/Phase-5-Parent-Analytics/P5-08-BE.md` (14 tasks), `…/P5-02-BE.md` (4 tasks), `…/P5-01-BE.md` (5 tasks).
- **Reference handler (mirror exactly):** `e:\Wrokspace\Learnexia\backend\src\Modules\Identity\Learnexia.Modules.Identity.Application\Features\Users\Queries\AdminGetUserActivity\GetUserActivitySummaryQueryHandler.cs` — fans out to 5 Gamification seams with per-seam try/catch graceful degradation.
- **Verified during planning:**
  - Parent module has Api/Application/Domain/Infrastructure with an existing `ParentDbContext` + migrations (`20260524130240_InitialParent`) + a `Service/` dir — but **no `Jobs/` folder yet** (P5-01 job is net-new for this module).
  - Existing Parent controller `…\Parent.Api\Controllers\ParentController.cs` — route base `api/Parent`, `[Authorize(Roles="Parent,Admin,SuperAdmin")]`, resolves acting parent from JWT in handlers.
  - Hangfire job pattern reference: `…\Billing.Infrastructure\Jobs\SeatEnforcementJob.cs` + registration in `…\Billing.Api\BillingModule.cs` (`[DisableConcurrentExecution]`, thin job → service, fail-soft). Every module registers its recurring jobs in its `*Module.cs`.
  - **Billing `GetBalanceAsync` side effect CONFIRMED:** `…\Billing.Infrastructure\Services\CreditSpendService.cs` docstring — "Creates the `ChildDailyUsage` row on first use." A parent-triggered read via this method **writes**. → P5-08-BE-4 must be pure-read (see Blockers).
  - **`Ai.IStudentWeakAreasQuery` is subject-scoped** (`GetWeakAreasAsync(studentId, Subject, ct)`) — the dashboard needs **all-subjects** weak areas. Seam shape decision is required (OQ-7, see Blockers).
  - `Shared.Contracts` is organized per-module (`Gamification/`, `Learning/`, `Billing/`, `Ai/`, `Parent/`) — these folders + each module's DI + `Program.cs` are the **serialized shared-file edit points**.

## Task inventory

### P5-08-BE — live read API (Parent module + cross-module seams)
| ID | Stack/agent | Summary | Est | Depends-on |
|---|---|---|---|---|
| P5-08-BE-1 | db-migration → backend-feature (Gamification) | `IStudentXpTimeSeriesQuery` seam (daily-XP series, 20-day trend, time-of-day buckets, totalXp, best/current streak, level) from `XpAward` ledger + streak/level state; impl in Gamification.Infrastructure + DI | 5h | index review (see DB stage) |
| P5-08-BE-2 | backend-feature (Learning) | `IStudentLearningStatsQuery` seam (lessons completed, attempts, time-learning = Σ `Attempt.DurationSeconds`, active-today, windowed counts) | 5h | index review |
| P5-08-BE-3 | backend-feature (Learning) | `IStudentMasterySummaryQuery` seam (per-subject mastery % + overall) from `StudentSkillMastery` | 4h | P3-09 (built) |
| P5-08-BE-4 | backend-feature (Billing) | `IChildEnergyUsageQuery` seam (per-child remaining/allocated/spent + weekly per-helper-kind usage from `CreditTransaction`); **pure-read — NO account/row bootstrap-on-read** | 4h | — |
| P5-08-BE-5 | backend-feature (Parent) | `ParentAnalyticsController` (`api/Parent/*`), `[Authorize(Roles="Parent,Admin,SuperAdmin")]`, `NewResult` envelope | 3h | BE-1..4 |
| P5-08-BE-6 | backend-feature (Parent) | **E1** `GET /Parent/Children/{id}/Progress` — fan-out level/xp/streak/mastery%/weakest/active-today/energy | 4h | BE-1..4, **P5-02-BE-2**, BE-5 |
| P5-08-BE-7 | backend-feature (Parent) | **E2** `GET /Parent/Family/Summary` — activeLearners/lessons/totalXp/bestStreak/badges (loops `GetChildIdsForParentAsync`) | 3h | BE-1,2, BE-5 |
| P5-08-BE-8 | backend-feature (Parent) | **E3** `GET /Parent/Children/{id}/WeeklyKpis` — rolling-7-day window + prior-window WoW deltas | 4h | BE-1,2, BE-5 |
| P5-08-BE-9 | backend-feature (Parent) | **E4** `GET /Parent/Children/{id}/SubjectMastery` | 2h | BE-3, BE-5 |
| P5-08-BE-10 | backend-feature (Parent) | **E6** `GET /Parent/Children/{id}/Reports` — daily XP, 20-day trend, time-of-day buckets | 3h | BE-1, BE-5 |
| P5-08-BE-11 | backend-feature (Parent) | **E7** `GET /Parent/Children/{id}/Energy` | 2h | BE-4, BE-5 |
| P5-08-BE-12 | backend-feature (Parent + seam DI) | **E5** `GET /Parent/Children/{id}/WeakAreas` — consume the real all-subjects weak-area seam | 3h | **P5-02-BE-2/3**, BE-5 |
| P5-08-BE-13 | backend-feature (Parent) | **E8** `GET /Parent/Children/{id}/Activity` — read-time compose recent events (XP awards, lessons, badges, energy) | 4h | BE-1,2,4, BE-5 |
| P5-08-BE-14 | api-tester + security-auditor | status/envelope/validation/auth + **cross-family IDOR** on every route; empty/first-week states; child-JWT rejected | 5h | BE-5..13 |

### P5-02-BE — weak-area detection (Learning)
| ID | Stack/agent | Summary | Est | Depends-on |
|---|---|---|---|---|
| P5-02-BE-1 | backend-feature (Learning) | Weak-area detector service — rank weak skills/subjects from `StudentSkillMastery` (`NeedsReview`/mastery deficit) + recent `Attempt` error rate → severity + subject + skill + suggested action | 6h | P3-09 (built) |
| P5-02-BE-2 | backend-feature (Learning + seam) | Real impl of the weak-areas read seam (replaces `EmptyWeakAreasQuery` placeholder). **Seam-shape decision required** (re-wire subject-scoped `Ai.IStudentWeakAreasQuery` vs new all-subjects `Shared.Contracts` read seam — see Blockers OQ-7) | 3h | BE-1 |
| P5-02-BE-3 | backend-feature (DI) | Re-wire DI: bind the seam to the real impl, replacing `EmptyWeakAreasQuery` (keep empty fallback only behind a flag if needed) | 1h | BE-2 |
| P5-02-BE-4 | api-tester (unit/integration) | Detector ranking correctness, empty/first-week → no weak areas (not an error), severity thresholds | 4h | BE-1..3 |

### P5-01-BE — scheduled weekly report (Parent) + `WeeklyReport` table
| ID | Stack/agent | Summary | Est | Depends-on |
|---|---|---|---|---|
| P5-01-BE-1 | **db-migration** (Parent) | `WeeklyReport` entity + migration — per (child, weekStartUtc): XP earned, skills improved, weak-area snapshot (JSON), recommendations, generatedAt; **unique (child, weekStart)**; `parent` schema, Npgsql, with Designer | 3h | — |
| P5-01-BE-2 | backend-feature (Parent) | Report generator service — for a child + prior week, aggregate XP/skills-improved/weak-areas via the P5-08 seams + P5-02; build recommendations; **idempotent upsert** of the `WeeklyReport` row | 6h | P5-08-BE-1..3, **P5-02-BE-2**, P5-01-BE-1 |
| P5-01-BE-3 | backend-feature (Parent) | Hangfire weekly job — sweep active children, generate prior-week report each (fail-soft, `[DisableConcurrentExecution]`); register in `ParentModule` (net-new `Jobs/` folder) | 3h | BE-2 |
| P5-01-BE-4 | backend-feature (Parent) | **E9** `GET /Parent/Children/{id}/WeeklyReport` (latest or by week) — IDOR-guarded; first-week → "no activity yet", not an error | 3h | BE-1, P5-08-BE-5 |
| P5-01-BE-5 | api-tester + security-auditor | generation idempotency, no-activity week, IDOR, envelope/auth | 4h | BE-1..4 |

**Total: 23 implementation tasks (~93h) + 3 test/audit task-bundles.**

## Dependency order

```
                        ┌─────────────────── FOUNDATION (cross-module seams) ───────────────────┐
 (DB index review)      P5-08-BE-1 (Gamification)   P5-08-BE-2 (Learning)   P5-08-BE-3 (Learning)
 P5-01-BE-1 (migration) P5-08-BE-4 (Billing, pure-read)        P5-02-BE-1 → P5-02-BE-2 → P5-02-BE-3 (Learning weak-area + seam)
                        └───────────────────────────────────────────────────────────────────────┘
                                                  │ (all seams live)
                 ┌────────────────────────────────┼───────────────────────────────────┐
                 ▼                                                                       ▼
        P5-08-BE-5 (controller)                                              P5-01-BE-2 (report generator)
                 │                                                                       │
   E1/E2/E3/E4/E5/E6/E7/E8 handlers  (BE-6..13, BE-12)                       P5-01-BE-3 (Hangfire job)
                 │                                                            P5-01-BE-4 (E9 read endpoint)
                 ▼                                                                       │
        P5-08-BE-14 (api-tester + security-auditor)  ◄────── E9 route folds in ─────────┘
                 │                                                            P5-01-BE-5 (api-tester + security-auditor)
                 ▼
            reviewer gate (whole wave)
```

**Critical path (longest chain):** `P5-02-BE-1 (detector, 6h) → P5-02-BE-2 (seam, 3h) → P5-02-BE-3 (DI, 1h) → P5-08-BE-6 (E1 fan-out, 4h) → P5-08-BE-14 (api-tester + security-auditor, 5h) → reviewer`. The weak-area chain (P5-02) is the pacing item because **E1 and the P5-01 report both consume it** — start P5-02-BE-1 on day 1 alongside the other seams.

**Foundation rule:** the four read seams (BE-1/2/3/4) + the P5-02 detector chain are the foundation; **every Parent fan-out handler and the P5-01 report depend on them.** No handler can be dispatched until its required seams compile and are DI-registered.

## Execution batches

### Batch 0 — DB stage (db-migration) — run FIRST, partly parallel with Batch 1 seams
- **`P5-01-BE-1` (migration):** create `WeeklyReport` entity in `Parent.Domain` + `IEntityTypeConfiguration` in `Parent.Infrastructure/Persistence/Configurations` + `dotnet ef migrations add AddWeeklyReport` against `ParentDbContext` (**Npgsql, `parent` schema**), commit the migration **with its `.Designer.cs`** and the updated `ParentDbContextModelSnapshot.cs`. Unique index on `(ChildId, WeekStartUtc)`; loose `int ChildId` (no cross-module FK); weak-area snapshot as `jsonb`.
- **Index review (no schema change expected for E1–E8):** db-migration verifies/【adds if missing】 the indexes that back the new windowed aggregates — `XpAward (StudentXpProfileId, OccurredAtUtc)`, `Attempt (StudentId, CompletedAt)` filtered `Status=Completed`, `CreditTransaction (…, OccurredAtUtc)` filtered on spend rows. Each added index is a migration **in its own module** and must be sequenced before that module's seam is load-tested. If all indexes already exist, record "verified, no change."
- **Gate:** reviewer (migration correctness — naming, schema, snapshot in sync, down-migration sane).

### Batch 1 — Cross-module read seams (backend-feature) — PARALLEL across modules, serialize shared-file edits
Each seam = interface in `Shared.Contracts/<Module>/` + impl in `<Module>.Infrastructure` + DI registration in that module's `DependencyInjection`/`*Module.cs` + unit tests.

- **1a `P5-08-BE-1` — Gamification** (`IStudentXpTimeSeriesQuery`)
- **1b `P5-08-BE-2` — Learning** (`IStudentLearningStatsQuery`)
- **1c `P5-08-BE-3` — Learning** (`IStudentMasterySummaryQuery`)
- **1d `P5-08-BE-4` — Billing** (`IChildEnergyUsageQuery`, **pure-read**)
- **1e `P5-02-BE-1 → BE-2 → BE-3` — Learning** (weak-area detector → real seam → DI re-wire) — internally **sequential**.

**Parallelism guidance:**
- 1a (Gamification), 1d (Billing), and the 1b+1c+1e (Learning) bundle touch **different modules** → can run as **3 parallel backend-feature agents** (one per module).
- **Within Learning**, 1b/1c/1e all edit `Learning.Infrastructure` + `Learning.Application` DI → assign to **one** Learning agent that does them in series (avoids merge conflicts on the same DI file). Their seam interfaces land in `Shared.Contracts/Learning/` (distinct files → no conflict).
- **Serialized shared-file edits (per PARALLELISM.md):** `Shared.Contracts/*.csproj` is shared but each seam adds a *new file* (safe). The real conflict points are **`Program.cs` / the per-module `*Module.cs` / Directory.Packages.props** if two agents touch them at once. Since each seam registers in **its own module's** DI, cross-module conflict is unlikely — but if the wave runs concurrently with another story's worktree, serialize any `Program.cs`/`Directory.Packages.props` edit.
- **Gate:** reviewer per seam (or one reviewer pass over the seam batch) — verify module isolation (no cross-module project ref), loose `int` ids, sentinel/empty-on-no-data, Option-C (impl in Infrastructure, Application EF-free), pure-read for BE-4.

### Batch 2 — Parent fan-out controller + handlers (backend-feature) — AFTER Batch 1 seams compile
All land in the **Parent** module; all share `ParentAnalyticsController` + the Parent DI → **one Parent agent, controller first then handlers** (handlers are independent of each other but share the controller file).

- **2a `P5-08-BE-5`** — `ParentAnalyticsController` scaffold (must land before its routes).
- **2b** then E-handlers (each: validate `childId` + window inline → resolve `parentId` from JWT → `IsParentOfChildAsync` gate → fan-out with per-seam try/catch → map DTO):
  - `P5-08-BE-7` (E2), `P5-08-BE-8` (E3), `P5-08-BE-9` (E4), `P5-08-BE-10` (E6), `P5-08-BE-11` (E7), `P5-08-BE-13` (E8) — depend only on Batch-1 seams.
  - `P5-08-BE-6` (E1) and `P5-08-BE-12` (E5) — **additionally depend on P5-02-BE-3** (weak-area DI live). Sequence these after 1e completes.
- **Gate:** reviewer (envelope `Successed`, `[Authorize(Roles=…)]`, IDOR gate present on every per-child route, localized EN/AR, `ILoggerManager`, graceful zero-state). **`security-auditor` runs in Batch 4** over the whole HTTP surface.

### Batch 3 — P5-01 scheduled report (backend-feature) — PARALLEL track, starts once Batch 1 seams + P5-01-BE-1 migration are done
Independent of Batch 2's handlers (shares only the `ParentAnalyticsController` for E9 — sequence E9 after BE-5).
- **3a `P5-01-BE-2`** — report generator service (consumes seams + P5-02 weak areas; idempotent upsert).
- **3b `P5-01-BE-3`** — Hangfire weekly job (net-new `Parent.Infrastructure/Jobs/` + register in `ParentModule`, mirror `SeatEnforcementJob`, `[DisableConcurrentExecution]`, fail-soft per child).
- **3c `P5-01-BE-4`** — E9 read endpoint on `ParentAnalyticsController` (needs BE-5).
- **Gate:** reviewer (job registration, idempotency, no-activity-week handling, IDOR on E9).

### Batch 4 — Runtime tests + security audit (api-tester → security-auditor) — AFTER Batches 2 & 3
- **`api-tester`** (`P5-08-BE-14` + `P5-02-BE-4` + `P5-01-BE-5`): WebApplicationFactory + Testcontainers (PostgreSQL). **Every HTTP route** — status codes, `BaseResponse` envelope, query validation (422/424 where applicable), auth (401/403), happy + zero/first-week paths. **Cross-family IDOR is mandatory:** parent A requesting parent B's `childId` → generic 403 (no distinction between "not your child" and "child not found"); **child-role JWT → rejected** on every parent route. Window-math assertions for E3/E6 (rolling-7-day + WoW). Idempotency + no-activity week for E9.
- **`security-auditor`** (MANDATORY — parent + child data, IDOR): every per-child route resolves `parentUserId` from JWT only (never from request); `IsParentOfChildAsync` called **before** any fan-out; generic 403 contract honored; **verify the Billing energy seam (BE-4) performs NO write on read**; no child PII leakage in errors/logs; localized messages don't leak existence. **Critical/High findings block the reviewer gate.**

### Final review gate — reviewer (whole wave)
Gates the full wave against the brief's AC1–AC8 + CONVENTIONS.md, **consuming the api-tester + security-auditor results.** Then `committer` opens the PR(s) on per-story branch(es) (`feat/P5-08-…`, and P5-01/P5-02 as agreed) — never on `main`.

## Review gates
| After batch | Gate(s) |
|---|---|
| Batch 0 (DB) | reviewer (migration + index correctness, snapshot in sync) |
| Batch 1 (seams) | reviewer (module isolation, sentinel contracts, Option-C, BE-4 pure-read) |
| Batch 2 (handlers) | reviewer (envelope, authz, IDOR gate present, localization) |
| Batch 3 (report) | reviewer (job registration, idempotency, no-activity week, E9 IDOR) |
| Batch 4 | **api-tester** (every route incl. cross-family IDOR) → **security-auditor** (MANDATORY, blocks on Critical/High) |
| Final | **reviewer** (AC1–AC8, consumes tester + auditor) → committer (PR) |

## Blockers / prerequisites (confirm before the relevant batch)

1. **[BLOCKS Batch 1, BE-4] Billing pure-read remediation — CONFIRMED REAL.** `CreditSpendService.GetBalanceAsync` "creates the `ChildDailyUsage` row on first use" (write-on-read). The new `IChildEnergyUsageQuery` (BE-4) **must not** call `GetBalanceAsync` for the read; it must derive remaining/allocated/spent + weekly usage via a non-mutating query path. *Decision needed:* (a) BE-4 reads the wallet/`CreditTransaction` directly read-only (recommended), or (b) add a pure-read overload to `ICreditSpendService`. **Either way, no parent-triggered read may write.** security-auditor verifies this in Batch 4.

2. **[BLOCKS P5-02-BE-2 + E5/E1] Weak-area seam shape (OQ-7) — DECISION NEEDED.** The existing `Ai.IStudentWeakAreasQuery` is **subject-scoped** (`GetWeakAreasAsync(studentId, Subject, ct)`); the dashboard needs **all-subjects** weak areas with severity. Options: (a) add a new **all-subjects** read seam in `Shared.Contracts/Learning/` and re-point the Ai placeholder consumer too, or (b) keep the Ai seam subject-scoped and add a separate Parent-facing seam. Recommendation: **(a)** a single Learning-owned all-subjects weak-areas read seam, with the subject-scoped Ai call delegating to it — confirm before P5-02-BE-2. *(This is a seam-shape choice, not a new design pattern — no rule-#8 approval needed.)*

3. **[Batch 2/3 — confirm, low risk] KPI window definitions (OQ-2).** Task files lock "rolling-7-day window + prior-window deltas" for E3 — proceed on that. Confirm the remaining definitions so E2/E3/E6 and the P5-01 report agree: "time learning" = Σ `Attempt.DurationSeconds`; "lessons completed" = distinct completed lessons vs completed attempts; WoW baseline = immediately prior 7 days. **Recommend the planner's defaults stand unless the lead says otherwise** — does not block Batch 1.

4. **[Not a blocker — note] Activity feed (E8) is read-time composed** from existing seams (XP awards, lessons, badges, energy) — no new table (lead's FULL-slice decision). `kind`/`category` enum literals must match the FE's `ACTIVITY_KIND` / `EnergyUsageStub.kind` — coordinate the JSON shape with the FE lead at api-client regen time.

5. **[Not a blocker — confirmed] No new module; P3-09 mastery is built.** `WeeklyReport` + read controller land in the **Parent** module (lead-locked). Mastery data (`StudentSkillMastery`, `MasteryEngine`) exists, so BE-3/P5-02 have real data — the gap was only the cross-module seam.

6. **[Hygiene — Batch 1] Serialize Program.cs / Directory.Packages.props edits** if any other story's worktree runs concurrently (PARALLELISM.md). Within this wave, each seam registers in its own module's DI, so intra-wave conflict is limited to the **Learning** trio (assigned to one agent) and the **Parent** handlers (assigned to one agent).

## Definition of done

**Per batch**
- **Batch 0:** `AddWeeklyReport` migration applies cleanly to a fresh `Learnexia` DB (Npgsql, `parent` schema), unique `(ChildId, WeekStartUtc)`, Designer + snapshot committed; index review recorded (added-or-verified). reviewer PASS.
- **Batch 1:** all four read seams + the weak-area seam compile, are DI-registered, return sentinel/empty on no-data, have unit tests; **no cross-module project reference**; BE-4 proven write-free; `EmptyWeakAreasQuery` replaced by the real impl. reviewer PASS.
- **Batch 2:** E1–E8 routes live on `ParentAnalyticsController`, each returns `BaseResponse<T>` (`Successed`), `[Authorize(Roles="Parent,Admin,SuperAdmin")]`, calls `IsParentOfChildAsync` before fan-out, degrades to clean zero-state, localized EN/AR. reviewer PASS.
- **Batch 3:** `WeeklyReport` generator is idempotent per (child, week); Hangfire job registered + fail-soft; E9 read IDOR-guarded with no-activity-week handling. reviewer PASS.
- **Batch 4:** api-tester green on every route incl. **cross-family IDOR + child-JWT rejection + window math + E9 idempotency**; security-auditor reports **no Critical/High** (esp. IDOR + BE-4 write-free). 

**Overall (tied to brief AC1–AC8)**
- **AC1** every endpoint returns the envelope and is parent-scoped (`[Authorize]`). **AC2** `parentUserId` from JWT only; `IsParentOfChildAsync` on every per-child route; generic 403. **AC3** clean zero/first-week states, never 404/500. **AC4** module isolation — all cross-module reads via `Shared.Contracts`, no FK. **AC5** localized + `ILoggerManager` + Option-C. **AC6** DTOs cover every field in the FE contract. **AC7** weak areas derive from mastery `<50%` (`NeedsReview`) + recent accuracy, carry severity, resolved areas drop off. **AC8** fan-out reads run concurrently where safe; per-child endpoint within NFR-1.
- All HTTP routes covered by api-tester; security-auditor clean; reviewer PASS on the wave; committer has opened the PR(s) per story on `feat/<StoryID>` branches.

**FE contract (for awareness — this lead builds none of it):** `apps\student-app\app\(parent)\_components\parentDashboardStubs.ts` is the field-level gold contract; `P5-05-FE` swaps stubs 1:1. BE DTO field names should align with the stub literals (or the FE maps them); `frontend-e2e-tester` re-runs parent flows once wired — **other lead's work.**

---
Plan ready — dispatch Batch 0 (db-migration: `WeeklyReport` + index review) and Batch 1 (cross-module read seams) in parallel.
