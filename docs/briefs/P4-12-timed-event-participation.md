# Pipeline Brief — P4-12 Timed-event participation

## Summary & traceability
- **Task (one line):** Add a per-child `TimedEventParticipation` aggregate to the Gamification module — lazily created on a child's first qualifying action inside an active timed-event window, accruing progress toward an event target, completing once with a reward through the existing XP engine, and exposing eligibility/participant read seams + per-student lifecycle integration events that P9-12 (timed-event nudges) consumes.
- **User story:** `user-stories/Phase-3-Gamification/P4-12-timed-event-participation.md` (source of truth).
- **Task file:** `tasks/Backend/Phase-3-Gamification/P4-12-BE.md` (BE-1 … BE-8).
- **FR-IDs:** FR-GM-9 (timed events — SRS §4.6). **Extends P4-11** (`TimedEvent` + scope + window + `IXpBoostCalculator` + `IActiveTimedEventsQuery`). **Pairs with P9-12** (nudges).
- **BRD goal:** G3 (engagement / habit loop) — turns a passive XP multiplier into a completable challenge with a reward and re-engagement hooks.
- **Epic / phase / sprint:** Gamification Module · Phase 3 — Gamification (Week 5) · built post-MVP alongside Phase 9.
- **Product-decision overrides in play:** child-scoped (parent-driven onboarding); no teacher role. Neither conflicts with this story.

## Business context & value
- **Who benefits:** the **student** (a timed event becomes a goal they can join, watch climb, and finish for a reward — not invisible background math); indirectly the **parent** (progress/completion is observable via existing parent-read seams downstream) and **ops** (config-tunable mechanics).
- **Value:** P4-11 shipped timed events as a **platform-wide** XP multiplier with start/end + scope, but with **no per-child participation, progress, or completion**. Nothing can answer "who's in / who's close / who finished," and the timed-event **nudges (P9-12) have no recipient** — confirmed in code by `Modules/Notifications/.../Reengagement/TimedEventNudgeDeferralNote.cs`, which explicitly defers the per-child nudge because "there is no 'active student list' seam … no `IActiveStudentQuery` or equivalent in `Shared.Contracts`." P4-12 supplies exactly that seam plus the per-student lifecycle events.
- **Success measured by:** participation rows created on first qualifying action (no upfront fan-out); progress accrual matches qualifying actions; single clean completion + reward; eligibility/participant seams return correct cohorts; lifecycle events emitted post-commit and consumed by P9-12.

## Acceptance criteria (testable)
1. **Lazy create.** No `TimedEventParticipation` rows exist for a timed event at activation. A row is created for a child only on that child's **first qualifying action inside `[StartUtc, EndUtc)`** — verified by asserting zero rows post-activation and exactly one row after a single qualifying action.
2. **Progress accrual.** Each subsequent qualifying in-window action increases `Progress`; `Progress` **clamps at `Target`** and never exceeds it.
3. **Window discipline.** Actions with `occurredAtUtc >= EndUtc` (or `< StartUtc`) do **not** create rows or accrue progress. After `EndUtc` the row is frozen (no further mutation).
4. **Single completion + reward via existing engine.** When `Progress` first reaches `Target`, `State` transitions in-progress → completed exactly once, `CompletedUtc` is set, and the completion reward is granted through **`StudentXpProfile.ApplyAward` / the existing XP chokepoint** (no parallel reward path). A second qualifying action after completion does not re-reward.
5. **Idempotency / no double-count.** Re-delivery of the same origin event does not double-accrue progress (mirror the mission `MissionProgressLog` idempotency backstop or the XP `HasXpAwardAsync` pre-check). The participation accrual composes with the existing boost application without changing awarded XP amounts.
6. **Eligibility + participant read seams.** `IEligibleStudentsForTimedEventQuery` returns a **scope-matched, bounded, on-demand cohort** (no materialized rows, no blind all-active blast). A participant/progress query returns current participants with progress. Both live in `Shared.Contracts/Gamification`, return **opaque ids + numeric scalars only** (no PII), and never return null (empty list sentinel).
7. **Per-student lifecycle integration events.** Progress-milestone (halfway), ending-soon/at-risk (close-but-incomplete near `EndUtc`), and completed events are emitted **post-commit, fail-soft, opaque ids only**, each with a documented record shape consumable by P9-12.
8. **Config-driven dials.** Target default, halfway %, ending-soon lookahead, and eligibility scope/window are bound from config (`TimedEventParticipationOptions`) and tunable without a deploy.

---

## Current-state investigation (verified, with citations)

### 1. `TimedEvent` entity — `Modules/Gamification/.../Domain/Entities/TimedEvent.cs`
Fields: `Code` (unique, ≤64), `NameEn/NameAr`, `DescriptionEn/Ar`, `StartUtc`, `EndUtc`, `Multiplier` (numeric(4,2), [1.00..5.00]), `Scope` (enum), `IsActive` (sweep latch), `CreatedAtUtc`. **No `Target`/goal field exists.** Mutated only by the sweep job (`Activate`/`Deactivate`) and P7-13 admin (`AdminUpdate`). EF config + CHECK constraints in `.../Persistence/Configurations/TimedEventConfig.cs`.
→ **The story's "progress toward the event target" has no backing field today — this is a real design decision (see Resolution A).**

### 2. `TimedEventScope` + scope interpretation — `Domain/Enums/TimedEventScope.cs`, `Infrastructure/Features/Events/Boost/XpBoostCalculator.cs`
Scope = `AllXp=1` (honored), `MissionXp=2` / `LeagueXp=3` (forward-compat, **not wired** — calculator no-ops on them). `XpBoostCalculator` filters active events to `Scope == AllXp` AND `StartUtc <= occurredAtUtc < EndUtc`, takes `max(Multiplier)` (no compounding), caps at `MaxMultiplierCeiling`, and is **fail-soft** (any seam exception → 1× pass-through, static warn log). It consults the cached `IActiveTimedEventsQuery` (`Shared.Contracts/Gamification/IActiveTimedEventsQuery.cs`, returns `ActiveTimedEventSnapshot` with `Scope` as `TimedEventScopeDto`).
→ **"Qualifying action" must align with this exact scope semantics so participation and the multiplier agree on what an in-window action is (see Resolution A).**

### 3. The XP-award path (the hook point) — `Infrastructure/Services/XpService.cs`
Both `AwardAnswerSubmittedXpAsync` and `AwardLessonCompletedXpAsync` run as `ICommand` handlers through the module's `UnitOfWorkBehavior` (single transaction per command). Inside each: idempotency pre-check (`_repo.HasXpAwardAsync`), profile row-lock (`_repo.AcquireProfileLockAsync`), Practice-Mode gate, then **`_boostCalc.GetEffectiveAmountAsync(base, reason, occurredAtUtc, ct)`** — which already loads the active timed events at `occurredAtUtc`. `MissionService.IncrementMissionProgressAsync` calls the same `_boostCalc` on mission completion.
→ **`XpService` is the single, correct insertion point for lazy-create + accrual.** It already runs in a transaction, already row-locks the profile, already loads active events. Accrual happens **after** XP is staged so it composes without touching the awarded amount (no double-count — participation accrual is a *separate* row, not an XP mutation; reward is granted only at completion).

### 4. Reward-grant + cross-module seam/republisher shapes
- **Reward engine (no parallel path):** `StudentXpProfile.RecordMissionCompleted(...)` (`Domain/Entities/StudentXpProfile.cs`) delegates to `ApplyAward` — the **single XP chokepoint** that mutates `TotalXp`, recomputes level, raises `XpAwardedDomainEvent` (leagues see it via `XpAwardedLeagueHandler`) + `StudentLeveledUpDomainEvent`, and writes an `XpAward` ledger row. `MissionService` wires this on mission completion (`MissionService.cs` §4e). **P4-12 completion reward mirrors this line-for-line** — call `ApplyAward` (via a new `RecordTimedEventCompleted(...)` method on the profile, or reuse `ApplyAward` with a new `XpReason`), write the `XpAward`, raise a domain event. No new reward mechanism.
- **Lazy-create + clamp + first-completion latch precedent:** `StudentMission` (`Domain/Entities/StudentMission.cs`) is the **exact mirror** — denormalized `Target` snapshot, `Progress` (`internal set`), `Status`, `CompletedAtUtc`, `ApplyProgress(increment, occurredAt, out completedNow)` that clamps to `Target` and latches completion once. EF config `StudentMissionConfig.cs` shows the **unique-index lazy-instantiation race backstop** + intra-module FK pattern to mirror.
- **Read-seam shapes:** `IActiveTimedEventsQuery` / `IStudentXpQuery` / `IStudentMissionsQuery` — interface in `Shared.Contracts/Gamification`, snapshot record (opaque ids, mirrored enums), impl in `Infrastructure/Queries` registered Scoped (`PostgresActiveTimedEventsQuery.cs`, `StudentMissionsQuery.cs`).
- **Republisher shape:** `MissionCompletedDomainEventRepublisher` / `TimedEventStartedRepublisher` — `INotificationHandler<TDomainEvent>` that publishes a `Shared.Contracts` integration event, **fail-soft** (catch + log, no rethrow), auto-registered via the Application `AssemblyReference` MediatR scan. Domain events are dispatched **post-commit** by `UnitOfWorkBehavior` (collect → clear → `DispatchAsync`).
- **Scan precedent (for ending-soon):** `Infrastructure/Jobs/StreakAtRiskJob.cs` — Hangfire recurring job, fresh DI scope, `AsNoTracking` query, `Take(500)` page guard with WARN, **per-student publish each in its own try/catch** (fail-soft), one event per distinct student. P9-06 added Pass 3 (weekly-mission reminder) to this same job. **P4-12 ending-soon is a new pass mirroring this** (close-but-incomplete participations near `EndUtc`).

### Load-bearing note — module-local Unit of Work (overrides CLAUDE.md rule 3 *inside Gamification only*)
The Gamification module has `Infrastructure/Behaviors/UnitOfWorkBehavior.cs`: for `ICommand<>` requests from the Gamification Application assembly it opens a transaction, runs the handler (handlers **stage only**), `SaveChangesAsync` once, commits, **then** dispatches domain events post-commit. So within a Gamification command, the lazy-create + progress write + completion + `XpAward` all commit atomically in **one** transaction — **no explicit transaction is needed in the handler/service** (the behavior owns it). This is the established pattern across XpService/MissionService and the `db-migration`/`backend-feature` agents must follow it (do **not** call `SaveChangesAsync` per write inside the service — stage and let the behavior commit). This is a deliberate module-local deviation from the global "no UoW" rule and is documented in ADR-0001/0002.

---

## Resolutions (recommend one each — confirm before db-migration)

### A. Qualifying action + target source  ⚠️ highest-risk unknown #1
**Qualifying action — recommendation:** define it **per scope, aligned with the multiplier**, accruing **1 unit of progress per qualifying XP-earning action**:
- `AllXp` (the only wired scope today): **any in-window XP award** = +1 progress. The natural accrual point is `XpService` (both award methods) right after the XP row is staged, because that's already where the boost is applied and the active-event list is loaded.
- `MissionXp` / `LeagueXp`: **forward-compat, not wired in this story** — exactly as `XpBoostCalculator` leaves them no-op. Accrual filters to the matching scope only; participation engine ignores `MissionXp`/`LeagueXp` events until a future story, identical to the calculator's current stance.

Rationale: keeping "qualifying action" identical to "action the multiplier applies to" means progress and multiplier never disagree, and it reuses the existing active-event load in `XpService` (no new scope interpreter, no new event subscriptions → no new design pattern, rule 8 satisfied). **Progress = count of qualifying actions** (an integer), mirroring `StudentMission.Progress` (count-based), not raw XP — simpler, scope-stable, and avoids coupling the goal to multiplier-inflated XP.

**Target source — recommendation (lowest friction): add a nullable `Target` (int) column to the `TimedEvent` definition**, defaulting from config when null.
- Add `int? ParticipationTarget` to `TimedEvent` (set via factory/`AdminUpdate`/seeder; nullable so existing P4-11 rows need no backfill). When null, the engine falls back to `TimedEventParticipationOptions.DefaultTarget`.
- The participation row **snapshots** the resolved target at create time (denormalized, exactly like `StudentMission.Target` from `MissionDefinition.Target`) so a mid-event admin edit doesn't move the goalposts for in-flight participants.
- Alternatives rejected: (i) **derive** target from multiplier/duration — opaque to ops, hard to tune, surprising; (ii) **config-only** (no per-event override) — every concurrent event shares one target, which breaks as soon as ops want a "10-lesson weekend" vs a "25-action Ramadan week." A nullable per-event column + config default gives both per-event control and a zero-friction default.

→ **Decision needed from lead:** confirm (1) accrual = **count of qualifying in-window XP actions** (not raw XP), and (2) target = **nullable `ParticipationTarget` column on `TimedEvent` + config default**. This is the single biggest schema decision and it touches the shared `TimedEvent` table (serialize — see batch plan).

### B. Lazy create + progress + completion lifecycle
**New aggregate `TimedEventParticipation`** (Gamification Domain), mirroring `StudentMission`:
- `int TimedEventId` — intra-module FK to `TimedEvents` (RESTRICT delete — catalog rows must not cascade-delete participation history; mirror `StudentMission → MissionDefinition`).
- `int StudentXpProfileId` (+ `StudentXpProfile` nav) — intra-module FK, **cascade delete** (mirror `StudentMission → StudentXpProfile`). Use `StudentXpProfileId` (consistent with `StudentMission`) rather than raw `StudentId`; the boundary seams project to opaque `studentId` via the profile nav (as `StreakAtRiskJob` does).
- `int Progress` (`internal set`, default 0), `int Target` (snapshot at create), `TimedEventParticipationStatus State` (`InProgress=1 → Completed=2`; default `InProgress` since the row is only born on first action), `DateTime JoinedUtc`, `DateTime? CompletedUtc`, denormalized `DateTime EventEndUtc` (snapshot for the ending-soon scan without a join, mirroring `StudentMission.PeriodEndUtc`).
- **Unique index** `(TimedEventId, StudentXpProfileId)` — lazy-instantiation race backstop (mirror `UX_StudentMissions_…`; catch `GamificationUniqueConstraintException` → treat as already-joined).
- Index for the ending-soon scan: `(State, EventEndUtc)` (mirror `IX_StudentMissions_Status_PeriodEndUtc_MissionType`).
- **Factory** `Create(profile, timedEventId, target, eventEndUtc, joinedUtc)`.
- **`ApplyProgress(int incrementBy, DateTime occurredAtUtc, out bool completedNow)`** — verbatim shape of `StudentMission.ApplyProgress`: no-op if already `Completed`; clamp `Progress` to `Target`; latch `Completed` + set `CompletedUtc` + `completedNow=true` once. Caller (XpService) also guards `occurredAtUtc < EndUtc` before calling (window discipline AC3) and raises the lifecycle domain events.
- **No cross-module FK** (rule 1). Derives from `CreationAuditedEntity` like `StudentMission`.

**Lifecycle in `XpService`** (both award methods), inside the existing transaction, after the XP row is staged:
1. Load active events at `occurredAtUtc` (already loaded by `_boostCalc`; consider returning them from a shared helper to avoid a second query — see Open Question OQ-2).
2. For each scope-matching active event with `StartUtc <= occurredAtUtc < EndUtc`: get-or-lazy-create the participation row (profile already row-locked → race-safe; unique index is the backstop), then `ApplyProgress(+1, occurredAtUtc, out completedNow)`.
3. On `completedNow`: grant reward via the existing engine (`ApplyAward` + `XpAward` row, mirror `MissionService` §4e) and raise the **completed** domain event; on crossing the halfway threshold raise the **progress-milestone** domain event. Domain events dispatch post-commit (UoW).
- **Idempotency:** the simplest correct approach is to reuse the existing per-command idempotency already guarding the XP award (the whole command is skipped on duplicate delivery via `HasXpAwardAsync`), so accrual inherits it for free **as long as accrual is gated behind the same not-already-awarded path**. Confirm placement so a duplicate event that short-circuits the XP award also short-circuits accrual (no separate progress-log table needed for v1; flag OQ-3 if the reviewer wants a dedicated `TimedEventProgressLog` mirroring `MissionProgressLog`).

### C. Eligibility + participant read seams (`Shared.Contracts/Gamification`)  ⚠️ highest-risk unknown #2
**`IEligibleStudentsForTimedEventQuery`** — `GetEligibleAsync(int timedEventId, DateTime atUtc, CancellationToken)` → `IReadOnlyList<int>` opaque studentIds, computed **on-demand, no materialized rows**.

**How to bound the cohort without a blind all-active blast — recommendation:** define "scope-eligible" as **students with recent in-module activity**, using the **already-present in-module signal `StudentXpProfile.LastActivityDateUtc`** (and/or `LastAwardAtUtc`). Concretely: students whose `LastActivityDateUtc >= atUtc - EligibilityWindowDays` (config dial, default e.g. 7) — i.e. recently-active students who *would* earn qualifying XP if they engage during the window. This is the **same in-module signal `StreakAtRiskJob` already scans** (`StudentXpProfiles.Where(p => ... LastActivityDateUtc ...)`), so no new cross-module activity seam is required and module isolation holds. Bound it with the same `Take(500)` page guard + WARN convention.
- For `AllXp` scope, "recently active" is the right cohort (any XP action qualifies). For the forward-compat `MissionXp`/`LeagueXp` scopes, the cohort would narrow to students with recent missions/league activity — **deferred** with those scopes (not wired this story), matching the calculator's stance.
- **Why not all-active / not a cross-module seam:** the P9-12 deferral note rejected a blind broadcast precisely because there's no recipient list and no budget-aware bounding. Using `LastActivityDateUtc` gives a *bounded, recency-filtered, in-module* recipient list with zero new cross-module coupling. If the lead wants a stricter activity definition (e.g. an Analytics-module activity signal via `Shared.Contracts`), that's a heavier option — **flag OQ-1**; default is the in-module recency filter.

**`IStudentTimedEventParticipationQuery`** (participant/progress read) — `GetByStudentIdAsync(int studentId, int timedEventId)` → snapshot `(Progress, Target, State, JoinedUtc, CompletedUtc?)` for the FE card (BE-8 endpoint reads through this), plus optionally `GetParticipantsAsync(int timedEventId)` for ops/analytics. Mirror `IStudentMissionsQuery` shape: opaque ids, mirrored `TimedEventParticipationStatusDto` enum, never-null sentinel, Scoped impl in `Infrastructure/Queries`.

### D. Per-student lifecycle integration events (`Shared.Contracts/Gamification`)
All records: opaque ids + numeric scalars only, `EventId`/`OccurredOnUtc` header (mirror existing integration events), emitted via **republishers** (fail-soft) off domain events raised in the aggregate, dispatched **post-commit** by the UoW. **Emit points:**
- **`TimedEventParticipationProgressIntegrationEvent`** `(EventId, OccurredOnUtc, StudentId, TimedEventId, Code, Progress, Target)` — raised at the **accrual hook** when progress first crosses the halfway threshold (config `HalfwayThresholdPercent`, default 50). Latch a "milestone raised" so it fires at most once per participation (e.g. only when `previousProgress < halfway <= newProgress`).
- **`TimedEventParticipationEndingSoonIntegrationEvent`** `(EventId, OccurredOnUtc, StudentId, TimedEventId, Code, Progress, Target, MinutesRemaining)` — raised by the **ending-soon scan** (new pass; see batch plan) for participations with `State == InProgress` and `EventEndUtc` within `EndingSoonLookaheadHours` of now. Mirror `StreakAtRiskJob` Pass 2/3 exactly (per-student, fail-soft, `Take(500)`).
- **`TimedEventParticipationCompletedIntegrationEvent`** `(EventId, OccurredOnUtc, StudentId, TimedEventId, Code, RewardXp)` — raised at the **accrual hook** on `completedNow` (mirror `MissionCompletedIntegrationEvent`).

These three are exactly what P9-12 consumes (recipient = `StudentId`, content = scalars), unblocking the deferral recorded in `TimedEventNudgeDeferralNote.cs`.

### E. Config dials — `TimedEventParticipationOptions` (`Gamification:TimedEventParticipation`)
Mirror `MissionOptions`/`TimedEventOptions` shape (`const SectionName`, defaults valid out-of-the-box):
- `int DefaultTarget` (fallback when `TimedEvent.ParticipationTarget` is null) — default e.g. 10.
- `int HalfwayThresholdPercent` — default 50.
- `int EndingSoonLookaheadHours` — default e.g. 6.
- `int EligibilityWindowDays` (recency window for the eligibility cohort) — default 7.
- `string EndingSoonScanCron` (+ `TimeZoneId`) **only if** the ending-soon scan runs as its own job; if it's a new pass on `StreakAtRiskJob`, reuse `ReengagementOptions` for the cron and add only the lookahead dial there. **Recommendation: add the pass to `StreakAtRiskJob`** (mirrors how P9-06 added the weekly pass) rather than a new job — fewer moving parts, one daily re-engagement sweep. Flag OQ-4 if a finer cadence than the daily 18:00 sweep is needed for short events.

---

## Affected modules & data (new vs existing)
- **Module:** Gamification only. No new module. **No new cross-module references.**
- **New entities:**
  - `TimedEventParticipation` (aggregate) + `TimedEventParticipationConfig` (EF) → `gamification.TimedEventParticipations` table.
  - `TimedEventParticipationStatus` enum (Domain) + `TimedEventParticipationStatusDto` mirror (Shared.Contracts).
  - Domain events: `TimedEventParticipationProgressDomainEvent`, `...CompletedDomainEvent` (ending-soon is published directly from the scan, no domain event needed — mirror `StreakAtRiskJob`).
- **Existing entities changed:**
  - `TimedEvent` — **add nullable `ParticipationTarget` (int?)** column (+ factory/`AdminUpdate`/seeder param + `TimedEventConfig` mapping; optional CHECK `ParticipationTarget IS NULL OR ParticipationTarget > 0`). ⚠️ shared P4-11/P7-13 table — serialize.
  - `StudentXpProfile` — optionally add `RecordTimedEventCompleted(...)` method (mirror `RecordMissionCompleted`); no new columns.
  - `XpReason` enum — **add `TimedEventCompleted`** value (next int = 7) for the completion `XpAward` ledger row.
- **New `Shared.Contracts/Gamification`:** `IEligibleStudentsForTimedEventQuery`, `IStudentTimedEventParticipationQuery` (+ snapshot record + status DTO), 3 integration events.
- **Infrastructure:** `PostgresEligibleStudentsForTimedEventQuery`, `StudentTimedEventParticipationQuery` (Queries, Scoped); 2 republishers (Application/.../Republishers); ending-soon pass added to `StreakAtRiskJob`; repository methods for get-or-create participation + scan; one BE-8 controller action; DI registration in `DependencyInjection.cs` (shared file — serialize).

## Handoff → db-migration
- New table `gamification.TimedEventParticipations`: `Id` (identity PK), `TimedEventId` (int, FK → `TimedEvents`, **RESTRICT**), `StudentXpProfileId` (int, FK → `StudentXpProfiles`, **CASCADE**), `Progress` (int, default 0), `Target` (int), `State` (int, default 1 = InProgress), `JoinedUtc` (timestamptz), `CompletedUtc` (timestamptz, null), `EventEndUtc` (timestamptz) + audit columns from `CreationAuditedEntity`.
  - Unique index `UX_TimedEventParticipations_TimedEventId_StudentXpProfileId`.
  - Index `IX_TimedEventParticipations_State_EventEndUtc` (ending-soon scan).
  - Optional CHECK `CK_TimedEventParticipations_Progress` (`Progress >= 0 AND Progress <= Target`) — mirror TimedEvent CHECK style.
- Alter `gamification.TimedEvents`: add nullable `ParticipationTarget` (int, null) [+ optional CHECK]. **Nullable so no backfill of existing rows.**
- **Generate via `dotnet ef migrations add P4_12_AddTimedEventParticipation` against `GamificationDbContext`** (factory `GamificationDbContextFactory.cs`). **Never hand-write.** One migration covering both the new table and the `TimedEvent` column. UTC timestamptz columns throughout (`timestamp with time zone`), matching `StudentMissionConfig`.

## Handoff → backend-feature
- **Domain:** `TimedEventParticipation` aggregate (factory + `ApplyProgress` out-completedNow, mirror `StudentMission`); `TimedEventParticipationStatus` enum; 2 domain events; `TimedEvent.ParticipationTarget` + factory/`AdminUpdate`/seeder threading; `XpReason.TimedEventCompleted`; optional `StudentXpProfile.RecordTimedEventCompleted`.
- **Accrual hook:** extend `XpService.AwardAnswerSubmittedXpAsync` + `AwardLessonCompletedXpAsync` — after the XP row is staged and within the same transaction, scope-match active events, get-or-lazy-create participation (profile already row-locked), `ApplyProgress(+1)`, grant reward on completion via `ApplyAward`+`XpAward` (mirror `MissionService` §4e), raise progress/completed domain events. **Gate accrual behind the same not-already-awarded path** so duplicate deliveries don't double-accrue. **Do not call `SaveChangesAsync` in the service — stage only; UoW commits** (module-local UoW, see load-bearing note).
- **Republishers:** `TimedEventParticipationProgressDomainEventRepublisher`, `...CompletedDomainEventRepublisher` (mirror `MissionCompletedDomainEventRepublisher` — fail-soft, auto-registered).
- **Read seams (Infrastructure/Queries, Scoped):** `PostgresEligibleStudentsForTimedEventQuery` (recency filter on `StudentXpProfile.LastActivityDateUtc`, `Take(500)` guard), `StudentTimedEventParticipationQuery` (project to snapshot, never-null sentinel).
- **Ending-soon scan:** new pass in `StreakAtRiskJob` (mirror P9-06 Pass 3) — `State == InProgress` AND `EventEndUtc` within lookahead → publish `TimedEventParticipationEndingSoonIntegrationEvent` per student, fail-soft.
- **Config:** `TimedEventParticipationOptions` + bind in DI + appsettings defaults.
- **Endpoint (BE-8):** JWT-scoped controller action "my participation/progress for active events" on `TimedEventsController.cs`, reads via `IStudentTimedEventParticipationQuery`, returns `BaseResponse<T>` via `NewResult(...)` (`Successed`), `ILoggerManager`.
- **Rules:** module isolation (Shared.Contracts only, no cross-module FK); `BaseResponse<T>`/`Successed`; `ILoggerManager`; UTC everywhere; mirror existing shapes (no new pattern — rule 8).

## Handoff → frontend (P4-12-FE, separate FE lead — not built in this pipeline)
- Contract for the FE card/countdown: `GET` my active-event participation → `{ progress, target, state, joinedUtc, completedUtc? }` per active event, extending the P4-11-FE timed-event surface. Backend only delivers the endpoint + shape here; FE is owned elsewhere.

## Open questions / assumptions / risks
- **OQ-1 (HIGH — eligibility cohort definition):** Default recommendation bounds the cohort via the in-module `StudentXpProfile.LastActivityDateUtc` recency window (no new cross-module seam). Confirm this is acceptable vs a stricter Analytics-module activity signal (heavier; new `Shared.Contracts` seam). **This is the #2 highest-risk unknown.**
- **OQ-2 (perf):** Accrual in `XpService` needs the active-event list at `occurredAtUtc`. It's already fetched inside `_boostCalc.GetEffectiveAmountAsync`. Decide whether to (a) re-call the cached `IActiveTimedEventsQuery` directly in the service (one extra cached read per XP award — cheap, Redis-backed) or (b) refactor the boost calc to also return the matched events. Recommend (a) for minimal blast radius; it's a cached call.
- **OQ-3 (idempotency depth):** v1 inherits idempotency from the per-command XP idempotency (duplicate delivery skips the whole command incl. accrual). Confirm the reviewer doesn't require a dedicated `TimedEventProgressLog` table (mirror `MissionProgressLog`) for per-(participation, originEvent) dedupe. Recommend NOT adding it in v1 unless accrual can run on a path that *doesn't* short-circuit on the XP idempotency check.
- **OQ-4 (scan cadence):** Ending-soon as a pass on the daily `StreakAtRiskJob` (18:00 UTC) is coarse for short events (e.g. a 24h weekend boost). Confirm the daily cadence is acceptable, or whether a dedicated higher-frequency `TimedEventParticipationScanJob` is warranted (more moving parts). Recommend the shared daily pass for v1.
- **OQ-5 (target source — needs explicit sign-off):** Confirm Resolution A — nullable `ParticipationTarget` on `TimedEvent` + config default, and accrual = **count of qualifying actions** (not raw XP). **This is the #1 highest-risk unknown (schema + shared-table edit).**
- **Assumption:** Only `AllXp` scope is wired this story (mirrors P4-11 calculator); `MissionXp`/`LeagueXp` participation is forward-compat/no-op. Eligibility + accuracy for those scopes are deferred.
- **Risk (rule 8 — design pattern):** Nothing here requires a *new* design pattern — every piece mirrors an existing shape (`StudentMission` aggregate, `RecordMissionCompleted` reward, republisher, read-seam, `StreakAtRiskJob` scan). **No new pattern proposed.** If `backend-feature` finds it can't lazy-create cleanly inside `XpService` without a Strategy/Factory abstraction, it must **STOP and ask the lead** (do not introduce one unilaterally).
- **Risk (shared-file contention):** `TimedEvent.cs`/`TimedEventConfig.cs`/`TimedEventSeeder.cs` (P4-11/P7-13), `XpService.cs` (XP path), `StreakAtRiskJob.cs` (P4-09/P9-06), `DependencyInjection.cs`, `Shared.Contracts` — all shared. Serialize edits; run this story on its own `feat/P4-12` branch with no parallel sibling touching these files.

## Recommended pipeline order (first cut — `planner` finalizes)
1. **db-migration** (Batch 1): `TimedEventParticipation` table + EF config + `TimedEvent.ParticipationTarget` column; `dotnet ef migrations add`. *(Gate: build + migration applies.)*
2. **backend-feature** (Batch 2, after migration): domain aggregate + enum + domain events + `TimedEvent`/`XpReason` threading; `XpService` accrual hook + completion reward; republishers; read seams; `StreakAtRiskJob` ending-soon pass; `TimedEventParticipationOptions`; BE-8 endpoint; DI. *(Internally sequence: Domain → XpService accrual → seams/republishers/scan → endpoint.)*
3. **api-tester** (Batch 3, parallel-after BE): BE-8 endpoint integration test + lazy-create/accrual/completion/window/idempotency assertions; mirror `P4_11_StreakFreezeTimedEvents_IntegrationTests.cs`.
4. **security-auditor** (Batch 3, parallel): child-data + IDOR (JWT-scoped endpoint must only return the caller's participation), opaque-ids-only on seams/events, no PII leakage. *(Critical/High block.)*
5. **reviewer** (gate): against the 8 acceptance criteria + CONVENTIONS + api-tester + security-auditor.
6. **committer** (after PASS): `feat/P4-12-timed-event-participation` branch, conventional commit, push + open PR. Update `docs/dev/HANDOFF.md` in the same PR (new seams, the `ParticipationTarget` decision, the accrual hook location, and that this unblocks P9-12).

**No designer stage** — backend-only (FE is a separate lead's surface; only a JSON contract is handed off).
