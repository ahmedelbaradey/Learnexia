# Pipeline Brief — Unit of Work strategy + Gamification module scaffolding

> Analyzer output. Read-only brief; the rest of the pipeline (db-migration → backend-feature → reviewer; frontend later) executes against this. Two linked pieces of work are covered: (1) adopt a Unit of Work strategy for new modules and capture it as ADR + convention; (2) scaffold the Gamification module on that pattern.

## Summary & traceability

- **Task (1 line):** Adopt a deferred-commit Unit of Work for *new* backend modules (Catalog untouched) via a MediatR `UnitOfWorkBehavior`, capture it as ADR `0001` + a CONVENTIONS rule, then scaffold the event-driven **Gamification** module (XP, levels, badges, streaks, hearts, missions, leagues) on that pattern.
- **User story:** *As the platform, when a student completes a lesson / submits an answer, gamification state (XP, level, streak, hearts, badges, missions, league standing) updates atomically and reliably so the student feels rewarded and a daily habit forms.*
- **SRS FR-IDs:**
  - Gamification: **FR-GM-1** (XP + level), **FR-GM-2** (streaks), **FR-GM-3** (hearts), **FR-GM-4** (badges), **FR-GM-5** (missions), **FR-GM-6** (leagues), **FR-GM-7** (event-driven fan-out on `LessonCompleted`/`AnswerSubmitted`).
  - NFRs: **NFR-2** (scalability / event-driven background processing), **NFR-8** (maintainability — modular monolith, CQRS, deterministic engines), **NFR-7** (rewarding <10-min sessions), **NFR-1** (API <500ms — gamification writes must stay off the hot path where heavy), **NFR-4** (auth/child-data).
  - UoW work itself is an architecture/maintainability concern supporting **NFR-8** and the atomicity needed by FR-GM-7; it is not a standalone FR.
- **BRD goals:** **G1** (engagement — XP, hearts, leagues), **G3** (daily habit — streaks, missions). Secondary: **G5** (scalable, event-driven platform).
- **TASK_BREAKDOWN epic:** **B5 — Gamification module (P4, event-driven)** — covers B5.1 (domain/integration events + handlers), B5.2 (XP/level + XPTransaction), B5.3 (streak + hearts), B5.4 (badges), B5.5 (missions), B5.6 (leagues + weekly job). Also touches **D2.3** (Gamification schema) and **O1.3** (background jobs infra for leagues/streak resets). The UoW decision is a cross-cutting foundation that B5 is the first consumer of.

## Business context & value

- **Who benefits:** the **student** (primary). Gamification is Learnexia's core differentiator — "success comes from habit loops, gamification, emotional design," per BRD §1. Parents benefit indirectly via XP/progress surfaced in weekly reports (FR-PA-1, out of scope here).
- **Value:** drives D1/D7 retention, sessions/day (G1) and streak length / daily-mission completion (G3) — the headline KPIs.
- **Success measurement:** XP awarded per event, streak length, mission completion rate, league participation. These are the analytics events B6 will later consume; gamification must emit/record them cleanly.
- **Architectural principle (BRD §8 / FR-AI-6):** deterministic engines make *decisions*; AI only generates content. Gamification is **100% deterministic** — no AI in this module.

## Why UoW first (the linkage)

Gamification is the first place the "no Unit of Work" gap (CONVENTIONS §13, architecture §12) actually bites: a single `LessonCompleted` reaction must atomically write an `Attempt`-derived XP award, update `StudentProfile` (XP total, level, streak, hearts), insert an `XPTransaction`, and possibly award a `StudentBadge` and advance a `StudentMission`. With the current Catalog `GenericRepository` (which calls `SaveChangesAsync` inside every write method — verified at `GenericRepository.cs:47-84`), a mid-sequence failure leaves partial state. So the UoW decision must land **before** Gamification handlers are written.

## Acceptance criteria (reviewer gates)

**Unit of Work (ADR + convention):**
1. **ADR exists and is accepted** — `docs/dev/adr/0001-unit-of-work.md`. *(Status: already drafted and Accepted, dated 2026-05-21 — needs user confirmation, not authoring. See Open Questions.)*
2. **CONVENTIONS.md carries the rule** — a new-module UoW rule is added/updated (today §8 and §13 still state a flat "No Unit of Work"; they must be amended to: Catalog = per-call commit (unchanged); **new modules = deferred commit via `UnitOfWorkBehavior`**). This is **not yet done** and is in scope.
3. **`UnitOfWorkBehavior<TRequest,TResponse>`** is implemented in the new module, constrained to `ICommand<>`, registered **after** `ValidationBehavior` in the MediatR pipeline. Queries never open a transaction or commit.
4. **New-module repositories do not call `SaveChangesAsync`** — they only `Add/Update/Remove` on the `DbSet`; the behavior commits once per command.
5. **Atomic rollback proven by test:** an integration/unit test issues a multi-write command that fails on the *last* write and asserts **none** of the earlier writes persisted (transaction rolled back). This is the headline reviewer gate.
6. **Catalog is untouched** — no edits to Catalog projects, `GenericRepository`, or its DI. Reviewer diff must show zero Catalog changes.
7. **Audit stamping still works** — `CreatedBy/UpdatedAt` are stamped by the module `DbContext.SaveChangesAsync(int userId)` override at flush time (not by hand in handlers).
8. **Events publish after commit** — domain/integration events are dispatched only after the transaction commits (or via outbox), never on uncommitted state.

**Gamification module:**
9. New `gamification` module with the standard 4-project Clean/Onion layout (Api/Application/Domain/Infrastructure), own `GamificationDbContext` on its **own PostgreSQL schema `gamification`**, `UseNpgsql`, per-schema `__EFMigrationsHistory`.
10. Entities + migration for: `StudentGamificationProfile` (XP total, level, streak count, hearts, last-activity), `XPTransaction` (ledger), `Badge` + `StudentBadge`, `Mission` + `StudentMission`, `League` + `StudentLeagueStanding` (or equivalent). No cross-module FKs — `StudentId` is a plain `int`.
11. **Event-driven (FR-GM-7):** the module **consumes** a cross-module `LessonCompletedIntegrationEvent` (from `Shared.Contracts`) and fans it out to XP, streak/hearts, badge, and mission handlers. (`AnswerSubmitted` may be a later increment.)
12. Deterministic XP rule applied (FR-GM-1: e.g. correct +10 / quiz +20 / lesson +50 / streak bonus +30 — exact values are an open question) and level computed from XP.
13. The lesson-completion reaction is a **single command** committed atomically through `UnitOfWorkBehavior` (ties to criterion 5).
14. Module isolation respected: gamification references no other module's projects; only `Shared.Kernel` + `Shared.Contracts`.
15. Build passes; module registered in Host via `AddGamificationModule(configuration)`.

**Explicitly deferred (not gated here):** leagues weekly promotion/demotion *job* (B5.6, needs O1.3 background-jobs infra), missions scheduler, hearts auto-regeneration timer, gamification UI (F5), analytics consumption (B6).

## Affected modules & data

### New module: `gamification` (schema `gamification`)

| Entity | New? | Purpose | Key fields | Relationships |
|---|---|---|---|---|
| `StudentGamificationProfile` | New | Per-student gamification state | `StudentId` (int, no FK), `XpTotal`, `Level`, `StreakCount`, `HeartsRemaining`, `LastActivityAt` | 1 per student |
| `XPTransaction` | New | Append-only XP ledger | `StudentId`, `Amount`, `Reason`, `CreatedAt` | many per student |
| `Badge` | New | Badge catalog (rule-based) | `Name`, `EarnCondition`, `Rarity` | catalog |
| `StudentBadge` | New | Earned badges | `StudentId`, `BadgeId`, `EarnedAt` | student × badge |
| `Mission` | New | Daily/weekly mission templates | `Type`, `Objective`, `RewardXp`, `ExpiresAt`, `TargetSkillId?` | catalog |
| `StudentMission` | New | Mission instance per student | `StudentId`, `MissionId`, `Status`, `ProgressPercentage` | student × mission |
| `League` + `StudentLeagueStanding` | New | Weekly leagues (Bronze→Diamond) + standings | tier, week, rank, xp-in-week | student × league/week |

> SRS §6 models gamification fields partly on `StudentProfile` (XP/level/streak/hearts live there). Because that `StudentProfile` belongs conceptually to the **Learning/Identity** side and does not exist yet, the brief proposes a module-owned `StudentGamificationProfile` keyed by `StudentId` (int, no cross-module FK) to honor module isolation. **Confirm with user** whether gamification state lives in its own module profile (recommended, isolation-clean) vs. on a shared StudentProfile. See Open Questions.

### Existing, reused
- `Shared.Kernel` — base entities (`FullAuditedEntity`), CQRS markers (`ICommand<>`/`IQuery<>`), `ValidationBehavior`, `ICurrentUserService`, `IGenericRepository`, domain-event abstractions (`Entity<TId>` with `RaiseDomainEvent`).
- `Shared.Contracts` — add the new `LessonCompletedIntegrationEvent` here (pattern: `ProductPublishedIntegrationEvent`, `IIntegrationEvent : INotification` with `EventId` + `OccurredOnUtc`).
- Identity, Notifications, Catalog — **not modified**.

### The Learning-module dependency (critical sequencing finding)

Gamification is **event-driven on lesson completion**, but **`LessonCompleted` and its source entities do not exist**:
- `Student`, `Lesson`, `Skill`, `StudentSkillMastery` are all flagged **New** in architecture §16 and SRS §7 — the Learning module (epic B2) and Assessment (B3) have **not been built**. Catalog is still demo Product/Category scaffolding.
- There is **no `LessonCompletedIntegrationEvent`** in `Shared.Contracts` today (only `UserRegistered*` and `ProductPublished*`).
- Therefore Gamification cannot receive a real lesson-completion signal until Learning/Assessment emit one.

**Recommendation — do NOT build the full Learning module first as a blocker.** Instead:
1. **Define the contract now.** Add `LessonCompletedIntegrationEvent` (StudentId, LessonId, SkillId, AccuracyPercentage, CorrectAnswerCount, CompletedAtUtc — fields the XP/streak/badge rules need) to `Shared.Contracts`. This is the seam; it lets Gamification be built and tested independently.
2. **Stub the producer for testing.** Gamification's reaction can be exercised by publishing the integration event from a test harness / a temporary internal endpoint, the same way Notifications is meant to consume `UserRegisteredIntegrationEvent`. No Learning module code is required for Gamification to be functionally complete and testable against the contract.
3. **Sequence Learning before wiring the real producer.** The *real* publisher of `LessonCompleted` lands when the Learning/Assessment modules are built (B2/B3, phase P2) — which per TASK_BREAKDOWN precede Gamification (P4) anyway. So in product order Learning does come first; but for *this* scaffolding task, the contract + stub is the minimal foundation and Learning is **not** a hard blocker.

**Minimal foundation Gamification requires:** (a) the UoW decision applied (ADR + behavior), (b) the `LessonCompletedIntegrationEvent` contract in `Shared.Contracts`, (c) a `StudentId` it can trust (int, no FK). Nothing else from Learning is strictly needed to scaffold and unit-test the module.

## Handoff → db-migration

- **Create `gamification` module skeleton** mirroring Catalog's 4-project layout; `GamificationDbContext : DbContext` with `public const string Schema = "gamification";`, `HasDefaultSchema(Schema)`, `UseNpgsql`, and `MigrationsHistoryTable("__EFMigrationsHistory", Schema)`.
- **Entities** (all derive from `FullAuditedEntity`, int surrogate keys, **no cross-module FKs** — `StudentId`/`SkillId` are plain ints): `StudentGamificationProfile`, `XPTransaction`, `Badge`, `StudentBadge`, `Mission`, `StudentMission`, `League`, `StudentLeagueStanding`. Field lists per the table above + SRS §6.
- **Relationships within schema:** `Badge`→`StudentBadge`, `Mission`→`StudentMission`, `League`→`StudentLeagueStanding`; `StudentGamificationProfile` is standalone keyed by `StudentId` (unique index on `StudentId`). XPTransaction indexed by `StudentId`.
- **Migration:** generate the initial migration in `Infrastructure/Migrations/`. Note CONVENTIONS §13 — **no startup auto-migrate** for non-Identity modules; apply via `dotnet ef database update` manually.
- **Seed (optional, confirm):** a starter `Badge` set and `League` tiers (Bronze/Silver/Gold/Diamond) may be seeded; defer if rules unconfirmed.
- Use EF conventions (like Catalog) unless a config is genuinely needed.

## Handoff → backend-feature

**A. Unit of Work plumbing (do first, in the new module):**
- Implement `UnitOfWorkBehavior<TReq,TRes> : IPipelineBehavior<TReq,TRes> where TReq : ICommand<TRes>` per the ADR skeleton (`docs/dev/adr/0001-unit-of-work.md` lines 26-43): inject `GamificationDbContext` + `ICurrentUserService`; `BeginTransactionAsync` → `next()` → `SaveChangesAsync(userId)` → `CommitAsync`; roll back on exception (await-using transaction handles dispose-rollback).
- Register it in the module's `AddGamificationApplication()` **after** `ValidationBehavior` (order: Validation → UnitOfWork → handler).
- New-module repositories/services **must stage only** (no `SaveChangesAsync`). Do **not** reuse Catalog `BaseService.AddAsync`'s save-at-call-time coupling (ADR Consequences) — either stage-only repos or handlers use repos directly.
- **Amend `docs/dev/CONVENTIONS.md`** §8 and §13 to document the new-module deferred-commit rule (criterion 2). Keep the Catalog "per-call commit" description but scope it to Catalog.

**B. Integration event contract:**
- Add `LessonCompletedIntegrationEvent(Guid EventId, DateTime OccurredOnUtc, int StudentId, int LessonId, int SkillId, int AccuracyPercentage, int CorrectAnswerCount) : IIntegrationEvent` to `Shared.Contracts` (new folder `Learning/`). Mirror `ProductPublishedIntegrationEvent`.

**C. Gamification feature work (CQRS, per CONVENTIONS):**
- **Event handler:** `LessonCompletedIntegrationEventHandler : INotificationHandler<LessonCompletedIntegrationEvent>` that translates the event into a single internal command (e.g. `ApplyLessonCompletionRewardsCommand`) sent via MediatR — so it flows through `UnitOfWorkBehavior` and commits atomically. (Do not do multi-writes directly in the notification handler outside a UoW boundary.)
- **Command:** `ApplyLessonCompletionRewardsCommand : ICommand<BaseResponse<...>>` whose handler: computes XP (deterministic rule), upserts `StudentGamificationProfile` (XP, level, streak, hearts), inserts `XPTransaction`, evaluates badge rules → `StudentBadge`, advances `StudentMission` progress. All staged; behavior commits once.
- **Queries:** `GetStudentGamificationProfileQuery`, `ListStudentBadgesQuery`, `ListStudentMissionsQuery`, league standings query — return `BaseResponse<T>` / paginated; **not** validated by behavior (validate inputs in-handler if needed).
- **Handlers** inherit `BaseResponseHandler`, inject `IServiceManager`/`IRepositoryManager` + `IMapper` + `ILoggerManager`; wrap bodies in try/catch and return `ServerError<T>` on exception (Catalog pattern). Success flag spelled **`Successed`**.
- **AutoMapper profiles** per aggregate; **FluentValidation** validator for the command.
- **Endpoints:** Gamification controller(s) under `/api/Gamification/...` (MVC, like Catalog) for the read queries; the reward write is event-triggered, not a public endpoint (confirm whether any admin/manual XP grant endpoint is wanted).
- **Outbox consideration:** for MVP, in-process MediatR dispatch of the integration event is acceptable (matches current Notifications design). The ADR mandates *publish-after-commit*; if durability of the lesson→gamification signal becomes a requirement, add an **Outbox** (persist event row in the same transaction, dispatch post-commit). Flag as a follow-up, not MVP-blocking — but the handler must not react to uncommitted state.
- **Auth:** add `[Authorize(policy)]` deliberately if securing read endpoints; note permission policies are generated for Catalog only today — adding a `Gamification` module to `Claims.GenerateModules()` is optional and should be a conscious choice.

## Handoff → frontend (deferred)

No frontend work in this cycle. For context only: TASK_BREAKDOWN **F5** (Reward screen, Badge collection, League, Missions, Hearts/Practice mode) and **F5.2** motion specs consume this module's read APIs (`GetStudentGamificationProfile`, badges, missions, league standings). Frontend is not started (no app in repo) and is correctly later (P4). API response shapes will be the `BaseResponse<T>` envelope (`Successed`/`data`).

## Open questions / assumptions / risks

> **RESOLVED (2026-05-21, by user):**
> 1. **ADR 0001 — approved** (on disk, Accepted).
> 2. **Streak — hard reset** (miss a day → streak resets to 0; no grace/freeze in MVP).
> 3. **Hearts — daily reset** (fixed pool refilled next day; no time-based regen; not premium-gated in MVP).
> 4. **Rule values — use SRS examples as defaults** (correct +10 / quiz +20 / lesson +50 / streak +30; Bronze→Diamond leagues), configurable later.
> 5. **Stub via event contract — approved** (define `LessonCompletedIntegrationEvent`; do not build Learning first).
> Secondary assumptions #6 (module-owned `StudentGamificationProfile`, no cross-module FK) and #7 (leagues/missions/hearts background jobs deferred — entities only this cycle) stand unless overridden.

**Top open questions for the user (decision-blocking or design-shaping):**
1. **Confirm ADR 0001.** It is already drafted as *Accepted* (2026-05-21). Confirm it stands as the agreed UoW decision before backend-feature implements `UnitOfWorkBehavior`. (Low risk — it matches the stated recommendation exactly.)
2. **Streak grace period (FR-GM-2)** — unspecified in SRS/BRD (BRD §10 Q4). Is there a grace day / freeze before a streak resets? Needed for the streak engine.
3. **Hearts regeneration (FR-GM-3)** — unspecified (BRD §10 Q4). How/when do hearts regenerate (time-based timer? per-day reset? premium-unlimited?) and how many per session? Affects schema + a possible background job.
4. **XP / level / badge / league rule values (FR-GM-1/4/6)** — SRS gives example XP values ("correct +10, lesson +50…") but not authoritative ones, nor level thresholds, badge earn-conditions, or league promotion/demotion counts. Need confirmed rules (or approval to use the SRS examples as defaults).
5. **Build Learning first vs. stub?** — Recommendation: **stub** via the `LessonCompletedIntegrationEvent` contract for this cycle; build the real producer when Learning/Assessment (B2/B3) land. Confirm this sequencing is acceptable rather than blocking Gamification on a full Learning module.

**Secondary / can proceed with stated assumption:**
6. Gamification state location — **assumption:** module-owned `StudentGamificationProfile` keyed by `StudentId` (no cross-module FK), not a shared `StudentProfile`. Confirm.
7. Leagues weekly job + missions scheduler + hearts-regen timer need **O1.3 background-jobs infra** (Hangfire/Quartz) which doesn't exist yet — **assumption:** these are deferred out of this scaffolding cycle; only the entities/standings model land now.
8. `AnswerSubmitted` event — **assumption:** out of scope this cycle; only `LessonCompleted` is wired. Confirm.

**Risks:**
- **R1 (low):** new modules and Catalog run two different commit models simultaneously — intentional per ADR, but reviewers must not "harmonize" Catalog. Mitigation: criterion 6 + CONVENTIONS amendment.
- **R2 (med):** if `BaseService` is reused as-is in the new module, its save-at-call-time behavior breaks the UoW guarantee. Mitigation: stage-only repos / refactor noted in handoff.
- **R3 (low):** no startup auto-migrate for new modules — migrations must be applied manually; easy to forget in a dev/test loop.
- **R4 (med):** in-process event dispatch means a crash between commit and event handling loses the gamification reaction; acceptable for MVP, outbox is the durable fix (flagged).

## Recommended pipeline order (sequential vs parallel)

```
1. USER DECISION GATE (before any code):
   - Confirm ADR 0001 (Q1) and the stub-vs-Learning sequencing (Q5).
   - Provide / approve defaults for streak grace, hearts regen, XP/level/badge/league rules (Q2-Q4).
     (Q2-Q4 can be answered with "use SRS examples" to unblock; engines are parameterizable.)

2. backend-feature (UoW + contract groundwork)  ── do first, small:
   - Amend CONVENTIONS.md (UoW rule).
   - Add LessonCompletedIntegrationEvent to Shared.Contracts.
   (No DB dependency yet — can start as soon as the gate clears.)

3. db-migration (gamification schema)  ── can run in PARALLEL with step 2:
   - Module skeleton + GamificationDbContext + entities + initial migration.

4. backend-feature (gamification features)  ── SEQUENTIAL, after 2 AND 3:
   - UnitOfWorkBehavior + registration, event handler, ApplyLessonCompletionRewards command,
     queries, validators, mapping, endpoints, atomic-rollback test.

5. reviewer  ── gates against the Acceptance Criteria above
   (esp. #5 atomic rollback proven, #6 Catalog untouched, #2 CONVENTIONS updated).

6. frontend  ── DEFERRED (later cycle, P4).
```

**Clear to proceed?** Mostly. The UoW half is **clear** (ADR already drafted/accepted; just needs confirmation). The Gamification half is **soft-blocked on product rules** (Q2-Q4) — but those can be unblocked immediately if the user says "use the SRS example values as defaults." The only true sequencing decision is Q5 (stub vs. build Learning first), and the recommendation is to **stub via the contract** so this cycle proceeds without the Learning module.
