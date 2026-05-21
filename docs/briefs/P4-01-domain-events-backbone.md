# Pipeline Brief — P4-01 Domain Events Backbone (event-driven fan-out)

> Analyzer output. Read-only brief; the rest of the pipeline (db-migration → backend-feature → api-tester → reviewer) executes against this. Source of truth = the user story `user-stories/Phase-4-Gamification/P4-01-domain-events-backbone.md` (acceptance criteria) + SRS FR-GM-7 / NFR-2 / NFR-8 + BRD goals + [ADR 0001](../dev/adr/0001-unit-of-work.md). Companion: the existing [uow-and-gamification brief](uow-and-gamification.md) (the Gamification *module* scaffolding that consumes this backbone).

## Summary & traceability

- **Task (1 line):** Build the **platform event backbone** so learning actions raise domain events and publish integration events that independent MediatR handlers (XP, badge, streak, analytics) fan out from — decoupled, after-commit, and suitable for background processing. This is a **technical enabler**, not a feature; it is the foundation P4-02..P4-07 and P5-03 build on.
- **User story:** P4-01 *"Emit learning domain events"* (5 pts, Technical Enabler, Phase 4 / Week 7, Gamification epic). Covers TASK_BREAKDOWN **B5.1**.
- **SRS / NFR IDs:**
  - **FR-GM-7** — event-driven fan-out on `LessonCompleted` / `AnswerSubmitted` (the headline requirement this story exists to satisfy).
  - **NFR-2** — scalability / event-driven background processing (the story explicitly requires "suitable for background/event-driven processing").
  - **NFR-8** — maintainability (modular monolith, loose coupling, deterministic engines).
  - Supporting: **NFR-1** (API <500ms — keep heavy fan-out off the request hot path).
- **BRD goals:** **G1** (engagement loops), **G3** (daily habit) are the *downstream* beneficiaries; the backbone itself supports **G5** (scalable, event-driven, modular platform). The story delivers no end-user value directly — it unblocks the modules that do.
- **TASK_BREAKDOWN epic:** **B5 — Gamification (event-driven)**, specifically **B5.1** (domain/integration events + handlers). Also underpins **O1.3** (background-jobs infra) and **B6/P5-03** (analytics consumes the same events).

### Task file gap (flag to lead)
- **There is no `tasks/Backend/Phase-4-Gamification/P4-01-BE.md`.** The per-stack task breakdown in `tasks/` currently only covers Phases 1–2; Phase-4 BE/FE task files do not exist. This brief therefore works **directly from the user story** (the source of truth) plus the verified codebase state. The `planner` should generate the task inventory from this brief; if the team wants a `tasks/` file for traceability, that is a separate housekeeping item.

## Business context & value

- **Who benefits:** indirectly the **student** (XP/streaks/badges react instantly and reliably to learning), and the **platform** (analytics in P5-03 reacts to the same events). No direct UI.
- **Value:** this backbone is the *spine* of Phase 4. Without it, every gamification reaction would be hard-wired into the learning/quiz flow (tight coupling, FR-GM-7 violated, NFR-8 regressed). It is a one-time architectural investment that 6+ downstream stories depend on.
- **Success measurement:** a single learning action (lesson completed / answer submitted) reliably triggers N independent reactions, each isolated (one failing handler does not break the others or the originating request), and the event path survives normal operation (and, at higher tiers, process crashes).
- **Deterministic principle (BRD §8 / FR-AI-6):** reactions are deterministic engines; no AI on this path.

## Verified codebase state (2026-05-21) — the platform is NOT event-driven yet

Confirmed against source. Only **scaffolding** exists; nothing actually publishes or dispatches:

| Concern | State (verified) | Evidence |
|---|---|---|
| Integration-event marker | **Exists** — `IIntegrationEvent : INotification` (`EventId`, `OccurredOnUtc`) | `Shared.Contracts/IIntegrationEvent.cs` |
| Integration-event contracts | **Exist** — `UserRegisteredIntegrationEvent`, `ProductPublishedIntegrationEvent` | `Shared.Contracts/Identity|Catalog/...` |
| Domain-event marker + base | **Exists** — `IDomainEvent : INotification`; `Entity<TId>` has `DomainEvents` / `RaiseDomainEvent` / `ClearDomainEvents` | `Shared.Kernel/DomainEvents/IDomainEvent.cs`, `Shared.Kernel/Entities/Entity.cs` |
| **Anything publishing** | **NONE** — no `IPublisher`, no `.Publish(...)` anywhere in the backend. The two integration-event contracts are never produced (only defined + one stub handler references one). | grep across `backend/` |
| **Dispatcher after SaveChanges** | **NONE** — no code reads `DomainEvents` after commit and publishes them. | grep |
| The one consumer | **Stub** — `UserRegisteredIntegrationEventHandler` is `=> throw new NotImplementedException();` | `Notifications.Application/IntegrationEventHandlers/...` |
| Domain-event capability of aggregates | **GAP** — `FullAuditedEntity → AduitedEntity → CreationAuditedEntity → BaseEntity`. It does **NOT** derive from `Entity<TId>`, so Catalog & all audited entities **cannot raise domain events**. Only **Notifications** uses `Entity<TId>`. | `Shared.Kernel/Entities/*.cs` |
| **MediatR registration** | **Per-module, isolated** — each module calls `AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly))` over **its own assembly only**. A `Publish()` from one module's container scope will **not** discover a handler living in another module's assembly. **This is the central wiring problem for cross-module fan-out.** | Catalog/Identity/Notifications `Application/DependencyInjection.cs` |
| Audit save | `DbContext.SaveChangesAsync(int userId)` is a **custom overload** (not an `override`); it stamps audit then calls `base.SaveChangesAsync()`. A real `SaveChangesInterceptor` hooks the *base* EF save and would fire correctly. | `CatalogDbContext.cs:31-65` |
| Unit of Work | **Not implemented** — ADR 0001 mandates `UnitOfWorkBehavior` but no behavior exists yet; Catalog still commits per repo call. After-commit publish depends on having a known commit boundary. | grep; ADR 0001 |
| Outbox | **NONE** — no table, no dispatcher, no `IOutbox*`. | grep |
| Async / scheduled infra | **NONE** — no Hangfire, no Quartz, no `IHostedService`, no `BackgroundService`. Aspire packages referenced but **unwired** (no AppHost project on disk). Redis is **sessions only** (and Program.cs currently uses in-memory cache). | grep; `Program.cs:33-35` |

**Bottom line:** P4-01 is genuinely a *from-scratch* event backbone, not a tweak. The contracts and `Entity<TId>` base are the only pre-existing bricks.

## Tiered scope (the core of this brief)

The story's acceptance criteria are fully satisfiable by **Tier 1 alone**. Tiers 2–3 are reliability/async hardening that the story does **not** require but that ADR 0001 anticipates and that downstream P4 stories (leagues job, hearts reset, missions scheduler) will need. Recommendation: **scope P4-01 = Tier 1, with the Tier-2 Outbox seam designed-for but deferred unless the user opts in.**

### Tier 1 — In-process domain + integration events (RECOMMENDED for P4-01)
The minimum that satisfies every P4-01 acceptance criterion.

1. **After-commit dispatcher.** An EF Core `SaveChangesInterceptor` (`SavingChanges`/`SavedChangesAsync`) — or an equivalent step inside `UnitOfWorkBehavior` after `CommitAsync` — that, **after the transaction commits**, collects `DomainEvents` from tracked `Entity<TId>` aggregates, publishes each via MediatR `IPublisher`, then calls `ClearDomainEvents()`. Publish **after** commit (ADR 0001 §4), never before, never on rollback.
   - *Decision needed:* interceptor (`SavedChangesAsync`) **vs.** explicit publish inside `UnitOfWorkBehavior` after commit. The behavior approach is cleaner for "exactly once after commit, not on rollback" because the interceptor's `SavedChanges` fires per `SaveChanges` call, not per logical transaction. **Recommend: publish in `UnitOfWorkBehavior` after `CommitAsync`** (single, well-defined boundary), collecting domain events from the ChangeTracker before clearing. (This couples P4-01 to having `UnitOfWorkBehavior` — see sequencing.)
2. **Close the `FullAuditedEntity` domain-event gap.** Make event-raising aggregates domain-event-capable. Two options (open question): (a) make `Entity<TId>` capability available to audited aggregates by having the audited chain expose `DomainEvents`/`RaiseDomainEvent`; or (b) only the *new* event-emitting aggregates (Learning module, not built) derive from `Entity<TId>` and the backbone reads domain events from those. **Recommend not retrofitting Catalog** (ADR 0001 §1 — Catalog is throwaway scaffolding). The cleanest path: the backbone reads `DomainEvents` from any tracked `Entity<TId>`; new modules use `Entity<TId>`-capable aggregates. Confirm whether a shared `AggregateRoot` base (audit + domain events) should be added to `Shared.Kernel` for new modules.
3. **MediatR cross-module dispatch.** Resolve the per-module-assembly isolation so a published `IIntegrationEvent` reaches handlers in *other* modules. Options: register all module Application assemblies into a **single MediatR configuration** at the Host, or add a shared dispatch registration that scans all loaded module assemblies. This is **required** for FR-GM-7 fan-out (XP/badge/streak/analytics handlers live in different modules than the producer). **This is the most important and easily-missed wiring item.**
4. **Publish integration events at the real seams.** Where a real producer exists today, publish at the seam (e.g. Identity publishing `UserRegisteredIntegrationEvent` after user creation commits). Implement the **stub consumer** `UserRegisteredIntegrationEventHandler` (currently `NotImplementedException`) as a proof the end-to-end path works (it can do a minimal real action or a logged no-op — confirm scope).
5. **Define `LessonCompletedIntegrationEvent`.** Learning is not built, so add the **contract** to `Shared.Contracts` (fields: `EventId`, `OccurredOnUtc`, `StudentId`, `LessonId`, `SkillId`, `AccuracyPercentage`, `CorrectAnswerCount` — per the uow-and-gamification brief) and **stub the producer** (test harness / temporary internal endpoint). `AnswerSubmitted` — the story names it; recommend defining its contract too but wiring it is optional this cycle (open question). Per ADR/uow brief, do NOT block on building Learning.
6. **Handler isolation (story AC).** Fan-out must run each handler independently so one failing handler does not block the others or the originating request. MediatR `Publish` default (sequential, throw-on-first) does **not** satisfy this — needs a **custom `INotificationPublisher`** (parallel/independent, catch-and-log per handler) so a failing XP handler doesn't abort the badge/streak/analytics handlers. This directly maps to the story's "A failing handler does not block other handlers."

### Tier 2 — Reliability: transactional Outbox (DESIGN-FOR, DEFER unless opted in)
Lightweight per-module Outbox so cross-module integration events are **durable** and publish-after-commit survives a crash between commit and dispatch.
- Outbox table per module schema; event row persisted **in the same transaction** as the state change; a dispatcher publishes post-commit and marks dispatched.
- ADR 0001 §3 already names this ("when an event must not be lost, use the Outbox"). In-process MediatR (Tier 1) is explicitly acceptable for MVP there.
- **Not required by P4-01 acceptance criteria.** Recommend deferring the *table + dispatcher* but designing the Tier-1 publish step so an Outbox can slot in without rework (i.e. publish through an `IEventDispatcher` seam, not a raw `IPublisher` call sprinkled in handlers).

### Tier 3 — Async / scheduled: Hangfire or Quartz + IHostedService (OUT OF SCOPE for P4-01)
The outbox **dispatch loop** as a background service, plus scheduled jobs (league rollover, mission scheduling, hearts daily reset, report generation).
- None of these are named in P4-01's acceptance criteria; they belong to **O1.3** and later P4 stories (P4-05/06/07) + P5 reports.
- **Recommend: explicitly out of scope for P4-01.** Decide Hangfire-vs-Quartz when O1.3 / the first scheduled job lands. (Note: if Tier 2 Outbox *is* pulled into P4-01, it needs *a* dispatch loop — even a simple `IHostedService` poller — which drags a sliver of Tier 3 in. Another reason to keep P4-01 = Tier 1.)

## Acceptance criteria (reviewer gates)

Derived from the story (source of truth) + ADR 0001. The reviewer gates P4-01 on the **Tier 1** set:

1. **Published exactly once, after commit, not on rollback.** A raised domain event is published exactly once after the transaction commits; a command that throws/rolls back publishes **nothing**. (Headline gate — needs a test.)
2. **Cross-module fan-out works.** A published `IIntegrationEvent` is delivered to `INotificationHandler<>`s defined in *other* modules' assemblies (proves the MediatR isolation is resolved).
3. **Handler independence.** Multiple handlers subscribe to one event and run independently; a deliberately-failing handler does **not** prevent the other handlers from running and does **not** fail the originating request. (Maps to story AC bullet 3.) Verified by a test with a throwing handler.
4. **Right-place publishing.** `LessonCompleted` (and, if in scope, `AnswerSubmitted`) is published at the correct point in the (stubbed) learning/quiz flow — i.e. after the unit-of-work commit, via the dispatcher seam, not inline mid-handler. (Story AC bullets 1–2.)
5. **`LessonCompletedIntegrationEvent` contract exists** in `Shared.Contracts` with the agreed fields; a stub producer can emit it for testing without the Learning module.
6. **Stub consumer implemented.** `UserRegisteredIntegrationEventHandler` no longer throws `NotImplementedException`; it performs a defined (minimal) action proving the integration-event path is live.
7. **Background-suitable (NFR-2).** Fan-out is off the request's critical path / async-capable (e.g. parallel notification publisher, or queued) so heavy reactions don't block the API response. (Story AC bullet 4.)
8. **No domain-event capability retrofitted onto Catalog** (ADR 0001 §1 — Catalog untouched); the gap is closed via the new-module path / a shared aggregate base, not by editing Catalog.
9. **Conventions honored** — module isolation (cross-module only via `Shared.Contracts`), no cross-module FKs, `ILoggerManager`, `BaseResponse`/`Successed` where envelopes apply.

**Tier-2 gate (only if Outbox pulled in):** an integration event survives a process crash between commit and dispatch via the outbox (persisted in-tx, re-dispatched on restart, published exactly once).

## Affected modules & data

| Surface | New? | Notes |
|---|---|---|
| `Shared.Contracts` | **Add** `LessonCompletedIntegrationEvent` (+ optional `AnswerSubmittedIntegrationEvent`) under `Learning/` | mirror `ProductPublishedIntegrationEvent`; `IIntegrationEvent` |
| `Shared.Kernel` | **Add** the after-commit dispatch seam (`IEventDispatcher` / `IDomainEventDispatcher`), the custom `INotificationPublisher` (independent fan-out), and (decision) a shared domain-event-capable aggregate base for new modules | the backbone lives here so all modules share it |
| Host (`Learnexia.Host`) | **Modify** — unify/extend MediatR registration so cross-module handlers are discoverable; register the dispatcher + custom publisher; (Tier 2 only) register outbox dispatcher | `Program.cs`, module DI |
| Identity module | **Modify (if seam chosen)** — publish `UserRegisteredIntegrationEvent` after user-creation commit | proves producer path |
| Notifications module | **Modify** — implement the `UserRegisteredIntegrationEventHandler` stub | proves consumer path |
| `UnitOfWorkBehavior` | **New (likely prerequisite)** — ADR 0001 mandates it; the recommended after-commit publish hangs off its commit boundary. Not yet implemented anywhere. | see sequencing/open questions |
| **Tier 2 (deferred):** outbox table | New, per-module schema | only if opted in |
| **Tier 3 (out of scope):** Hangfire/Quartz, `IHostedService` | New | O1.3 / later P4 |

No new business data in Tier 1 (events are transient/in-process). Tier 2 adds an outbox table per module schema.

## Handoff → db-migration

- **Tier 1: no migration required** (in-process events are not persisted).
- **Concern to own regardless:** if the team adds a shared domain-event-capable aggregate base for new modules, confirm it introduces **no new columns** (domain events are `[NotMapped]` / not persisted) — the migration surface must stay empty. Verify `DomainEvents` is never mapped by EF (it's a `List` field on `Entity<TId>`; ensure any new base keeps it unmapped).
- **Tier 2 (only if opted in):** create an **outbox table per module schema** (`<schema>.outbox_messages`: `Id`, `OccurredOnUtc`, `Type`, `Content` (json), `ProcessedOnUtc?`, `Error?`). Initial migration in that module's `Infrastructure/Migrations/`, `MigrationsHistoryTable(... , Schema)`. No cross-module FKs. Note CONVENTIONS §13 — no startup auto-migrate for non-Identity modules; apply manually.
- **Interceptor/dispatcher registration is a backend-feature concern, not a schema concern** — but db-migration should be aware that a `SaveChangesInterceptor`, if used, must be added to each module `DbContextOptions` (`AddInterceptors(...)`), which touches each module's Infrastructure DI.

## Handoff → backend-feature

**A. After-commit dispatch (core):**
- Implement an `IDomainEventDispatcher` / `IEventDispatcher` seam in `Shared.Kernel`. Recommended: publish inside `UnitOfWorkBehavior` **after `CommitAsync`** — collect `DomainEvents` from tracked `Entity<TId>` aggregates in the ChangeTracker, publish each via `IPublisher`, then `ClearDomainEvents()`. (If instead an EF `SaveChangesInterceptor` is used, hook `SavedChangesAsync` and be careful it fires once per logical transaction, not per nested `SaveChanges`.)
- **Prerequisite:** `UnitOfWorkBehavior` (ADR 0001) is **not yet implemented**. If P4-01 lands before the Gamification module, P4-01 must either implement the behavior in `Shared.Kernel` (so it's reusable) or pick the interceptor route. Flag for planner — see Open Questions.

**B. Independent fan-out (story AC #3):**
- Implement a custom `INotificationPublisher` (MediatR 12+) that runs handlers independently and **catches + logs per-handler exceptions** (via `ILoggerManager`) so one failing handler doesn't abort the rest or the request. Register it in the unified MediatR config. Decide sequential-with-isolation vs. parallel (`Task.WhenAll`) — parallel best serves NFR-2 but watch DbContext scoping (each handler that writes needs its own scope/UoW).

**C. Cross-module MediatR wiring (FR-GM-7 enabler):**
- Resolve the per-module-assembly isolation. Register **all** module Application assemblies into one MediatR pipeline (Host-level), or add a shared registration that scans loaded module assemblies, so a `Publish(integrationEvent)` reaches handlers in any module. Verify with a cross-module test (producer in module A, handler in module B).

**D. Contracts + producer/consumer:**
- Add `LessonCompletedIntegrationEvent` (and optionally `AnswerSubmittedIntegrationEvent`) to `Shared.Contracts/Learning/`.
- Stub the **producer** (Learning not built) — a test harness or temporary internal endpoint that publishes `LessonCompletedIntegrationEvent`.
- Implement the **consumer** stub `UserRegisteredIntegrationEventHandler` (replace `NotImplementedException`).
- (Optional) publish `UserRegisteredIntegrationEvent` from Identity after user creation commits, to prove a real producer seam.

**E. Tier 2 (only if opted in):** `IOutbox` write in-tx + a dispatcher; persist event row in the same transaction, publish post-commit, mark processed.

**Conventions:** `ILoggerManager` (not `ILogger<T>`); module isolation; no cross-module FKs; do not edit Catalog; keep `BaseResponse`/`Successed` where envelopes apply.

## Handoff → api-tester

- If a temporary/internal endpoint is added to emit `LessonCompletedIntegrationEvent` (or to trigger `UserRegistered`), api-tester validates the end-to-end fan-out against the running API: one call → all subscribed handlers ran, a forced-failing handler did not break the others or the HTTP response (gates AC #1, #3, #7). If the producer is exercised purely via in-process tests (no HTTP surface), api-tester may be **N/A** — confirm with planner whether any endpoint is exposed.

## Handoff → frontend
- **None.** Backend-only technical enabler.

## Open questions / assumptions / risks

**Top open questions for the user (decision-shaping):**
1. **Tier scope for P4-01 — confirm Tier 1 only?** Recommendation: P4-01 = Tier 1 (in-process MediatR fan-out, after-commit, independent handlers). Outbox (Tier 2) designed-for but **deferred**; Hangfire/Quartz + hosted services (Tier 3) **out of scope** (→ O1.3 / later P4). Confirm, or pull the Outbox into P4-01 now (it then drags a minimal dispatch loop with it).
2. **Dispatch mechanism — `UnitOfWorkBehavior` after-commit vs. `SaveChangesInterceptor`?** Recommend after-commit publish inside `UnitOfWorkBehavior` (clean once-per-transaction boundary). But `UnitOfWorkBehavior` is **not implemented yet** — does P4-01 implement it (in `Shared.Kernel`, reusable), or does the Gamification scaffolding (uow-and-gamification brief) implement it first and P4-01 sequence after? This is the key **sequencing** decision.
3. **`AnswerSubmitted` in scope this cycle?** The story names both `LessonCompleted` and `AnswerSubmitted`. Recommend defining both contracts but wiring only `LessonCompleted` end-to-end now (define `AnswerSubmitted` contract, wire later). Confirm.
4. **How to close the `FullAuditedEntity` domain-event gap?** Recommend NOT retrofitting Catalog (ADR 0001). Options: (a) add a shared `AggregateRoot` base (audit + domain events) to `Shared.Kernel` for new modules; (b) only new `Entity<TId>` aggregates raise events and the dispatcher reads from those. Pick (a) or (b).
5. **Stub consumer behavior** — should `UserRegisteredIntegrationEventHandler` do a real action (e.g. create a welcome notification) or a logged no-op proving the path? And should Identity actually publish `UserRegisteredIntegrationEvent` now, or is the stubbed-producer test harness enough for P4-01?

**Assumptions (proceed unless overridden):**
- A1: P4-01 = Tier 1; Outbox/scheduled deferred (mirrors uow-and-gamification brief R4 + ADR 0001 "in-process acceptable for MVP").
- A2: Cross-module fan-out requires unifying MediatR registration — treated as in-scope (FR-GM-7 cannot work otherwise).
- A3: Learning module is **not** a blocker — contract + stubbed producer per ADR/uow brief.
- A4: Backend-only; no UI; no migration in Tier 1.

**Risks:**
- **R1 (high):** the **per-module MediatR assembly isolation** silently breaks cross-module fan-out. If missed, handlers in other modules never fire and FR-GM-7 fails despite a "working" publish. → Gate AC #2 with a cross-module test. **This is the most likely thing to be overlooked.**
- **R2 (med):** default MediatR `Publish` is sequential and throws on the first handler exception — violates story AC #3 (handler independence) unless a custom `INotificationPublisher` is added.
- **R3 (med):** publishing before commit (or on rollback) leaks uncommitted state to consumers. → Gate AC #1; publish strictly after `CommitAsync`.
- **R4 (med):** in-process dispatch loses the reaction on a crash between commit and handler run (no durability) — acceptable for MVP per ADR 0001; the Outbox (Tier 2) is the documented fix. Keep the dispatch behind an `IEventDispatcher` seam so Tier 2 slots in without rework.
- **R5 (low):** parallel fan-out + shared `DbContext` scope = concurrency bugs. Each writing handler needs its own scope/UoW.
- **R6 (low):** `UnitOfWorkBehavior` not existing yet creates a hidden dependency between this story and the UoW work — sequence explicitly.

## Recommendation: warrant a new ADR 0002 — Domain Events & Dispatch? **YES.**

This decision is **cross-cutting exactly like ADR 0001** (it touches every module, the Host MediatR wiring, the commit boundary, and the publish-after-commit ordering that ADR 0001 §4 already gestures at but does not specify). It has multiple non-obvious, contested choices that future agents will otherwise re-litigate:
- after-commit dispatch **mechanism** (interceptor vs. UoW-behavior),
- **independent fan-out** publisher (the non-default MediatR behavior),
- **cross-module MediatR registration** (the isolation fix),
- **in-process now / Outbox later** boundary and the `IEventDispatcher` seam,
- the **domain-event-capable aggregate base** decision.

Recommend authoring **ADR 0002 — Domain Events & Dispatch** (interceptor/behavior choice + strict after-commit ordering + cross-module dispatch + in-process-vs-outbox path), referencing and complementing ADR 0001. ADR 0001 §4 ("events publish after commit, via outbox where durability matters") is the natural hook. **This brief recommends the lead get user sign-off on the ADR 0002 direction before backend-feature implements**, the same way ADR 0001 was confirmed.

## Recommended pipeline order (first cut — planner finalizes)

```
0. USER / ADR DECISION GATE (before code):
   - Confirm Tier 1 scope (Q1), dispatch mechanism + UoW sequencing (Q2), AnswerSubmitted scope (Q3),
     aggregate-base approach (Q4), stub-consumer behavior (Q5).
   - Approve direction for ADR 0002 — Domain Events & Dispatch.

1. backend-feature (backbone, Shared.Kernel + Host) — sequential, small:
   - IEventDispatcher seam + custom INotificationPublisher (independent fan-out).
   - Unify cross-module MediatR registration in Host.
   - After-commit publish (in UnitOfWorkBehavior, implementing it in Shared.Kernel if not present).
   - (Decision Q4) shared domain-event-capable aggregate base.

2. backend-feature (contracts + producer/consumer) — can overlap step 1's tail:
   - Add LessonCompletedIntegrationEvent (+ optional AnswerSubmitted) to Shared.Contracts.
   - Stub producer (test harness / internal endpoint); implement UserRegistered consumer stub.

3. db-migration — only if Tier 2 Outbox is opted in (else N/A): outbox table per schema.

4. api-tester — if an HTTP trigger endpoint is exposed: validate end-to-end fan-out + handler isolation.

5. reviewer — gates against Acceptance Criteria (esp. #1 once-after-commit, #2 cross-module,
   #3 handler independence, #8 Catalog untouched).

6. committer — after reviewer PASS, on feat/P4-01-domain-events-backbone.
```

**Clear to proceed?** **Soft-blocked on a decision gate, not on missing information.** The codebase is fully understood and Tier 1 is well-scoped. Before planning, the lead should get the user to: (1) confirm Tier 1 scope + Outbox-deferral, (2) decide the dispatch mechanism and **whether P4-01 owns `UnitOfWorkBehavior`** (the one real sequencing dependency), and (3) approve the **ADR 0002** direction. With "Tier 1, publish-in-UoW-behavior, ADR 0002 approved, AnswerSubmitted contract-only," the pipeline is clear to plan immediately.
