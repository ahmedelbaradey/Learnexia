# Execution Plan — P4-01 Domain Events Backbone

## Source

| Artifact | Path |
|---|---|
| Pipeline Brief | `docs/briefs/P4-01-domain-events-backbone.md` |
| Companion Brief (UoW + Gamification) | `docs/briefs/uow-and-gamification.md` |
| User Story | `user-stories/Phase-4-Gamification/P4-01-domain-events-backbone.md` |
| ADR 0001 (Unit of Work) | `docs/dev/adr/0001-unit-of-work.md` |
| Per-stack task file | **NONE** — task inventory generated from brief + story (tasks/ covers Phases 1–2 only) |

---

## Resolved: who owns UnitOfWorkBehavior + the dispatcher

**P4-01 owns both.** Rationale:

1. `UnitOfWorkBehavior` does not yet exist anywhere in the codebase (verified: no behavior implements `IPipelineBehavior` that opens a transaction). ADR 0001 mandates it for every new module, but it is a *backbone* concern, not a Gamification-specific one.
2. The brief's recommended dispatch mechanism is: publish domain events **inside `UnitOfWorkBehavior` after `CommitAsync`**, not via a raw `SaveChangesInterceptor`. This coupling is intentional — a single, well-defined commit boundary guarantees publish-after-commit (AC #1) and publish-never-on-rollback.
3. The Gamification module (P4-02+) will *consume* the backbone P4-01 builds. It must not implement `UnitOfWorkBehavior` itself because: (a) the behavior needs to be reusable (each module gets its own typed instance but same pattern), and (b) the event-dispatch extension (`IEventDispatcher`) lives in `Shared.Kernel` and must be wired before any gamification handler can fan out.
4. Therefore, **the sequencing is**: P4-01 finishes the backbone (UnitOfWorkBehavior template in Shared.Kernel, IEventDispatcher seam, custom INotificationPublisher, unified MediatR registration, event contracts, Identity producer, Notifications consumer) → then Gamification (P4-02+) adds its module-specific UoW behavior instance + event handlers on top of the working backbone.

**`UnitOfWorkBehavior` placement:** The ADR 0001 skeleton is module-scoped (injecting `<Module>DbContext`). P4-01 adds a **reusable generic variant** or a clear pattern to `Shared.Kernel` (as a documented template, not a shared concrete class that creates a cross-module DbContext dependency). Each module instantiates its own concrete behavior by following the ADR pattern; P4-01 proves it works end-to-end by implementing it inside the **Identity module** (the only module with a real producer today) so the `UserRegistered` proof-of-life test can exercise the full commit→dispatch→fan-out path.

---

## Task Inventory

> Stack: all Backend (BE). No Frontend, no DB migration required (Tier 1 = no outbox table).
> Estimates are story-points at the task level (1 sp ≈ half-day focused effort).

| ID | Stack | Summary | Est | Depends-on |
|---|---|---|---|---|
| P4-01-BE-1 | BE | **ADR 0002 — Domain Events & Dispatch** (draft + lead sign-off gate). Document: dispatch mechanism (UoW-behavior after commit, not interceptor), independent fan-out publisher, cross-module MediatR registration approach, IEventDispatcher seam + Outbox-deferral, aggregate-base decision. | 1 sp | — |
| P4-01-BE-2 | BE | **`IEventDispatcher` seam in Shared.Kernel**. Add `IDomainEventDispatcher` interface + a default in-process implementation that calls `IPublisher.Publish` per domain event. Keep behind the seam so Tier-2 Outbox can slot in. | 1 sp | P4-01-BE-1 |
| P4-01-BE-3 | BE | **Custom `INotificationPublisher`** (independent fan-out). Implement a MediatR `INotificationPublisher` that runs handlers independently (parallel or sequential-with-isolation), catches + logs per-handler exceptions via `ILoggerManager`, so one failing handler does not abort the others or the originating request (AC #3). | 1 sp | P4-01-BE-1 |
| P4-01-BE-4 | BE | **Unify cross-module MediatR registration** in `Learnexia.Host` (`Program.cs` / `ServiceExtensions`). Register all module Application assemblies into a single MediatR configuration (or use `RegisterServicesFromAssemblies` across all module application assemblies) so `Publish()` reaches handlers in every module. Register the custom `INotificationPublisher` from P4-01-BE-3. | 1 sp | P4-01-BE-3 |
| P4-01-BE-5 | BE | **`AggregateRoot` base for new modules in Shared.Kernel** (event-capable audited aggregate). Add a `AggregateRoot` class (or amend the audited entity chain) so new modules can have aggregates that both carry audit fields AND raise domain events. Do NOT touch Catalog or existing `FullAuditedEntity`. Verify `DomainEvents` list is `[NotMapped]` / never mapped by EF. | 1 sp | P4-01-BE-1 |
| P4-01-BE-6 | BE | **`UnitOfWorkBehavior` pattern + Identity instance**. Implement `UnitOfWorkBehavior<TReq,TRes>` per ADR 0001 skeleton, in `Identity.Application` (as the first real instance). After `CommitAsync`, collect domain events from ChangeTracker's tracked `Entity<TId>` aggregates, call `IDomainEventDispatcher.DispatchAsync(events)`, then `ClearDomainEvents()`. Register after `ValidationBehavior`. This is the commit→dispatch engine. | 2 sp | P4-01-BE-2, P4-01-BE-4, P4-01-BE-5 |
| P4-01-BE-7 | BE | **`LessonCompletedIntegrationEvent` contract** in `Shared.Contracts/Learning/`. Fields: `EventId` (Guid), `OccurredOnUtc` (DateTime), `StudentId` (int), `LessonId` (int), `SkillId` (int), `AccuracyPercentage` (int), `CorrectAnswerCount` (int). Also add `AnswerSubmittedIntegrationEvent` contract (fields: same minus AccuracyPercentage; wire later). Mirror `ProductPublishedIntegrationEvent`. | 0.5 sp | P4-01-BE-1 |
| P4-01-BE-8 | BE | **Identity producer — publish `UserRegisteredIntegrationEvent`**. In the Identity command handler for user registration (or in the `UnitOfWorkBehavior` dispatch path), raise `UserRegisteredIntegrationEvent` as a domain event on the aggregate (or publish it directly via `IEventDispatcher`) after the user-creation commit. This proves the real-producer seam. | 1 sp | P4-01-BE-6, P4-01-BE-7 |
| P4-01-BE-9 | BE | **Implement `UserRegisteredIntegrationEventHandler`** (Notifications module). Replace the `NotImplementedException` stub with a minimal real action: log the user registration event (and optionally enqueue a welcome-notification `SendNotificationCommand` via MediatR `ISender`). Proves the cross-module end-to-end consumer path. | 1 sp | P4-01-BE-8 |
| P4-01-BE-10 | BE | **Stub `LessonCompleted` producer** — a temporary internal/admin endpoint (e.g. `POST /api/internal/test/lesson-completed`) that publishes `LessonCompletedIntegrationEvent` directly via `IPublisher` (bypassing any Learning module, for testing only). Allows api-tester + reviewer to exercise AC #1, #2, #3, #7 without building Learning. Tag with `[ApiExplorerSettings(IgnoreApi = true)]` or keep in a `#if DEBUG` block. | 1 sp | P4-01-BE-7, P4-01-BE-4 |
| P4-01-BE-11 | BE | **Unit / integration tests**. Write tests covering: (a) publish-after-commit, not-on-rollback (AC #1); (b) a cross-module handler receives the event (AC #2 — Identity publishes → Notifications handler runs); (c) a deliberately-failing handler does not prevent other handlers (AC #3); (d) `IEventDispatcher` seam is correctly wired. Use an in-memory / test-host approach (no external DB required for event dispatch tests). | 2 sp | P4-01-BE-9, P4-01-BE-10 |

**Total estimate: ~12 sp** (5-sp story = backbone infrastructure + core wiring; additional tasks reflect the breadth of the "from-scratch" backbone).

---

## Dependency Order

```
P4-01-BE-1  (ADR 0002 sign-off)
    └─► P4-01-BE-2  (IEventDispatcher seam)
    └─► P4-01-BE-3  (custom INotificationPublisher)
    └─► P4-01-BE-5  (AggregateRoot base)
    └─► P4-01-BE-7  (LessonCompleted + AnswerSubmitted contracts)
            │
    P4-01-BE-3 → P4-01-BE-4  (unify MediatR + register custom publisher)
            │
    P4-01-BE-2 + P4-01-BE-4 + P4-01-BE-5
            └─► P4-01-BE-6  (UnitOfWorkBehavior, commit→dispatch engine)
                    └─► P4-01-BE-8  (Identity producer: UserRegistered)
                            └─► P4-01-BE-9  (Notifications consumer)
                                    └─► P4-01-BE-11  (tests)

    P4-01-BE-7 + P4-01-BE-4
            └─► P4-01-BE-10  (stub LessonCompleted producer endpoint)
                    └─► P4-01-BE-11  (tests)
```

The critical path is: **ADR sign-off → dispatcher seam + custom publisher + aggregate base → UoW behavior → Identity producer → Notifications consumer → tests**.

---

## Execution Batches

### Pre-Batch: Decision Gate (lead/user action — no agent)

**Action required before any implementation:** Lead presents the brief's Open Questions to the user and collects sign-off on:
- Tier 1 scope confirmed (Outbox deferred).
- Dispatch mechanism: `UnitOfWorkBehavior` after `CommitAsync` (not interceptor).
- P4-01 owns `UnitOfWorkBehavior` and `IEventDispatcher` (Gamification module consumes it).
- `AnswerSubmitted`: contract-only this cycle, no wiring.
- `AggregateRoot` base: add to `Shared.Kernel` for new modules (not retrofit Catalog).
- `UserRegisteredIntegrationEventHandler`: minimal real action (log + welcome-notification command).
- **ADR 0002 direction approved** (document is the first deliverable).

**Gate: ADR 0002 approved before Batch 1 starts.** The `backend-feature` agent must not begin until the ADR is written and signed off.

---

### Batch 1 — ADR + Foundational seams (sequential; backend-feature agent)

**Agent:** `backend-feature` | **Model:** sonnet | **Tasks:** P4-01-BE-1, P4-01-BE-2, P4-01-BE-3, P4-01-BE-5, P4-01-BE-7

**Parallelism:** P4-01-BE-2, P4-01-BE-3, P4-01-BE-5, and P4-01-BE-7 are independent of each other and can be implemented in one pass after the ADR (P4-01-BE-1) is written and approved.

**Deliverables:**
- `docs/dev/adr/0002-domain-events-dispatch.md` (P4-01-BE-1)
- `Shared.Kernel`: `IDomainEventDispatcher` + default in-process impl (P4-01-BE-2)
- `Shared.Kernel`: `IsolatedNotificationPublisher : INotificationPublisher` with catch-per-handler + `ILoggerManager` (P4-01-BE-3)
- `Shared.Kernel`: `AggregateRoot` base (or audited domain-event capable base) — DomainEvents `[NotMapped]` (P4-01-BE-5)
- `Shared.Contracts/Learning/LessonCompletedIntegrationEvent.cs` + `AnswerSubmittedIntegrationEvent.cs` (contract-only) (P4-01-BE-7)

**Review gate:** `reviewer` checks Batch 1 before Batch 2. Specifically: ADR 0002 faithfully records the decisions locked above; seam interfaces are correctly placed in `Shared.Kernel`; contracts mirror `ProductPublishedIntegrationEvent`; `AggregateRoot` introduces no EF-mapped columns.

---

### Batch 2 — Host wiring + UoW behavior (sequential after Batch 1; backend-feature agent)

**Agent:** `backend-feature` | **Model:** sonnet | **Tasks:** P4-01-BE-4, P4-01-BE-6

**Sequence within batch:** P4-01-BE-4 (unify MediatR) must complete before P4-01-BE-6 (UoW behavior) is registered, since UoW registration in Identity depends on MediatR being correctly unified.

**Deliverables:**
- `Learnexia.Host/Program.cs` (or `ServiceExtensions`): unified MediatR registration scanning all module Application assemblies; `IsolatedNotificationPublisher` registered.
- `Identity.Application`: `IdentityUnitOfWorkBehavior` (or `UnitOfWorkBehavior<TReq,TRes>` per ADR pattern) that opens a transaction, runs the handler, calls `SaveChangesAsync`, commits, then calls `IDomainEventDispatcher.DispatchAsync(domainEvents)` (collected from ChangeTracker before/after SaveChanges).
- Registration in Identity DI: `ValidationBehavior` → `UnitOfWorkBehavior` → handler.

**Review gate:** `reviewer` checks Batch 2 before Batch 3. Specifically: unified MediatR scan includes Identity, Notifications, Catalog, and (when added) Gamification assemblies; behavior ordering is correct; publish happens strictly after `CommitAsync`; no Catalog files changed.

---

### Batch 3 — Producer + Consumer + Stub endpoint (sequential after Batch 2; backend-feature agent)

**Agent:** `backend-feature` | **Model:** sonnet | **Tasks:** P4-01-BE-8, P4-01-BE-9, P4-01-BE-10

**Parallelism:** P4-01-BE-9 (Notifications consumer) and P4-01-BE-10 (stub LessonCompleted endpoint) are independent of each other and can be written in parallel; both depend on P4-01-BE-8 being done or on the UoW behavior being wired.

**Deliverables:**
- `Identity.Application`: user-registration aggregate raises `UserRegisteredIntegrationEvent` (or event dispatcher publishes it) after commit.
- `Notifications.Application/IntegrationEventHandlers/UserRegisteredIntegrationEventHandler.cs`: replaces `NotImplementedException` with a minimal real action (log + optionally dispatch `SendNotificationCommand`).
- `Learnexia.Host` (or a dedicated `Internal` controller): `POST /api/internal/test/lesson-completed` stub endpoint that publishes `LessonCompletedIntegrationEvent`; tagged so it does not appear in prod Swagger.

**Review gate:** `reviewer` checks Batch 3 before Batch 4. Specifically: `NotImplementedException` is gone from `UserRegisteredIntegrationEventHandler`; the stub endpoint is clearly marked as internal/test-only; no cross-module project references introduced.

---

### Batch 4 — Tests + api-tester validation (sequential after Batch 3)

**Agent (tests):** `backend-feature` | **Model:** sonnet | **Task:** P4-01-BE-11
**Agent (api validation):** `api-tester` | **Model:** sonnet | **No new task ID** — exercises Batch 3 deliverables

**api-tester scope:** The stub endpoint (`POST /api/internal/test/lesson-completed`) and the Identity user-registration flow provide the HTTP surface. api-tester must assert:
1. `POST /api/internal/test/lesson-completed` → all subscribed handlers ran (no 5xx), response succeeds.
2. A forced-failing handler (inject one for the test) does not cause a non-2xx response and does not prevent other handlers from completing (AC #3 / AC #7).
3. `UserRegistered` path: create a user → `UserRegisteredIntegrationEventHandler` runs without throwing (AC #6, verified via logs or observable side-effect).
4. Rollback path: a command that fails mid-handler (simulated) → no event published (AC #1 — confirmed via absence of handler side-effects).

**Unit tests scope (P4-01-BE-11):**
- `publish-after-commit-not-on-rollback`: command that throws after staging writes → assert no domain event reached any handler.
- `cross-module-delivery`: publish `UserRegisteredIntegrationEvent` from Identity test host → `UserRegisteredIntegrationEventHandler` in Notifications assembly receives it (proves R1 fix).
- `handler-independence`: two handlers subscribed to one event; first handler throws; assert second handler still ran and response did not fail (proves AC #3, R2 fix).
- `dispatcher-seam`: `IDomainEventDispatcher` mock → verify `DispatchAsync` is called exactly once after commit, never before.

**Review gate:** `reviewer` final gate (see Definition of Done below).

---

### Batch 5 — Final Review + Commit (sequential after Batch 4)

**Agent:** `reviewer` then `committer` | **Model:** sonnet

- Reviewer runs full acceptance-criteria checklist (all 9 ACs from the brief).
- On PASS: committer stages and commits all changes on branch `feat/P4-01-domain-events-backbone` with a conventional commit message. No push unless lead requests.
- On FAIL: committer is blocked; reviewer returns specific failing ACs to lead for remediation.

---

## Stage Applicability

| Stage | In scope? | Rationale |
|---|---|---|
| `designer` | **No** | Backend-only technical enabler; no UI surface. |
| `db-migration` | **No (Tier 1)** | In-process events are not persisted; no new tables. If Tier-2 Outbox is ever pulled in, db-migration handles the outbox table per module schema. The `AggregateRoot` base must be verified to introduce no EF-mapped columns — but that is a backend-feature concern, not a migration concern. |
| `backend-feature` | **Yes** | Owns Batches 1–3 + test writing in Batch 4. |
| `api-tester` | **Yes (conditional)** | The stub endpoint (`/api/internal/test/lesson-completed`) is an HTTP surface. api-tester validates end-to-end fan-out and handler isolation against the running host. |
| `security-auditor` | **Assess — LOW, mark not needed** | No new public endpoints are added; the stub endpoint is internal/test-only. The `UserRegisteredIntegrationEvent` and `LessonCompletedIntegrationEvent` payloads carry `StudentId`/`LessonId` (int identifiers only) — no PII (no name, email, DOB, or child-data fields). The story does not touch auth/authz or file upload. Security-auditor stage is **not required** for this cycle, but the reviewer must confirm event payloads carry no PII and the stub endpoint is not reachable in production. |
| `reviewer` | **Yes** — after each meaningful batch | Gates after Batch 1 (seams), Batch 2 (UoW wiring), Batch 3 (producer/consumer), and final pass after Batch 4. |
| `committer` | **Yes** — after final reviewer PASS | On `feat/P4-01-domain-events-backbone`. |
| `frontend` | **No** | No frontend work this cycle. |

---

## Review Gates

| Gate | After | Key checks |
|---|---|---|
| Gate 1 | Batch 1 | ADR 0002 faithful to locked decisions; `IDomainEventDispatcher` in `Shared.Kernel`; `IsolatedNotificationPublisher` correctly logs per-handler; contracts mirror existing pattern; `AggregateRoot` no EF columns; Catalog untouched. |
| Gate 2 | Batch 2 | Unified MediatR scan includes all module Application assemblies; behavior ordering (`ValidationBehavior` before `UnitOfWorkBehavior`); publish **after** `CommitAsync` (not before, not inside `next()`); no Catalog changes. |
| Gate 3 | Batch 3 | `UserRegisteredIntegrationEventHandler` no longer throws; Identity raises/publishes the event; stub endpoint is internal-only; zero cross-module project references introduced. |
| Gate 4 (final) | Batch 4 | All 9 ACs verified (see Definition of Done); tests pass; api-tester report clean; ADR 0002 on disk and referenced in CONVENTIONS.md. |

---

## Blockers / Prerequisites

| # | Blocker | Owner | Resolution |
|---|---|---|---|
| B1 | **ADR 0002 sign-off (hard gate)** — the dispatch mechanism, fan-out publisher, and aggregate-base decisions must be approved before `backend-feature` writes a line of code. Without this, the agent will re-litigate the interceptor-vs-behavior and parallel-vs-sequential choices mid-implementation. | Lead → User | Draft ADR 0002 as the **first deliverable of Batch 1**; lead presents to user for approval before Batch 2 starts. |
| B2 | **UoW ownership decided** — resolved: P4-01 owns `UnitOfWorkBehavior` and `IEventDispatcher`; Gamification consumes them. If the user overrides this (e.g. "build Gamification first"), the plan sequencing changes. | Lead | Confirm at the Pre-Batch decision gate. |
| B3 | **`AnswerSubmitted` scope** — resolved (locked): contract defined this cycle, wiring deferred. If the user changes scope to "wire `AnswerSubmitted` end-to-end now," add a task P4-01-BE-12 and an `AnswerSubmittedIntegrationEventHandler` in Gamification (but Gamification module itself is not built yet, so this is only sensible if a stub handler is added to Notifications as a second proof). | Lead | Confirm at Pre-Batch gate. |
| B4 | **Gamification module scaffolding (P4-02+)** — the Gamification module exists in the repo (`Learnexia.Modules.Gamification.*`) with only enum files; it has no DI, no DbContext, no application layer. P4-01 must NOT implement Gamification feature work (that is P4-02+), but must ensure the backbone it delivers is immediately consumable by the Gamification module. The `AggregateRoot` base, `UnitOfWorkBehavior` pattern, `IEventDispatcher`, and unified MediatR are the contract P4-02+ depends on. | Backend-feature | P4-01 finishes first; Gamification feature work begins after Gate 4. |
| B5 | **CONVENTIONS.md not yet updated** for the new-module UoW rule (this was in scope per the uow-and-gamification brief, AC #2). P4-01 must ensure `docs/dev/CONVENTIONS.md` §8 and §13 are amended to document the new-module deferred-commit rule, alongside ADR 0002. | Backend-feature (Batch 2) | Include CONVENTIONS.md amendment in Batch 2 deliverables. |

---

## Definition of Done

### Per-batch DoD

**Batch 1 done when:**
- `docs/dev/adr/0002-domain-events-dispatch.md` exists, is status `Accepted`, and documents all locked decisions.
- `IDomainEventDispatcher` is in `Shared.Kernel` with a default in-process implementation.
- `IsolatedNotificationPublisher` is in `Shared.Kernel`; per-handler catch + `ILoggerManager` log.
- `AggregateRoot` base (or equivalent) is in `Shared.Kernel`; EF mapping verified as none.
- `LessonCompletedIntegrationEvent` + `AnswerSubmittedIntegrationEvent` contracts exist in `Shared.Contracts/Learning/`.
- Catalog files: zero changes.
- Gate 1 review: PASS.

**Batch 2 done when:**
- All module Application assemblies (Identity, Notifications, Catalog, Gamification — once it has an Application assembly) are included in a single MediatR registration at the Host.
- `IsolatedNotificationPublisher` is registered as the MediatR notification publisher.
- `UnitOfWorkBehavior` instance exists in `Identity.Application`, registered after `ValidationBehavior`; behavior opens a transaction, runs handler, saves, commits, then dispatches domain events via `IDomainEventDispatcher`.
- `docs/dev/CONVENTIONS.md` §8 + §13 amended to document new-module deferred-commit rule.
- Gate 2 review: PASS.

**Batch 3 done when:**
- Identity user-registration raises/publishes `UserRegisteredIntegrationEvent` after commit.
- `UserRegisteredIntegrationEventHandler` contains no `NotImplementedException`; it performs a minimal real action.
- `POST /api/internal/test/lesson-completed` endpoint exists, publishes `LessonCompletedIntegrationEvent`, is tagged internal/non-production.
- Gate 3 review: PASS.

**Batch 4 done when:**
- All unit/integration tests pass (commit→dispatch, cross-module delivery, handler independence, seam isolation).
- api-tester report: end-to-end fan-out confirmed; handler-isolation confirmed; rollback path confirmed.
- Gate 4 (final) review: PASS.

### Overall / Story-level DoD (tied to Story Acceptance Criteria)

| Story AC | How verified | Gate |
|---|---|---|
| `LessonCompleted` and `AnswerSubmitted` domain events published at right points | `LessonCompleted` end-to-end via stub endpoint; `AnswerSubmitted` contract exists, wire deferred (documented in ADR 0002). | Gate 3 + api-tester |
| Handlers (XP, badge, streak, analytics) subscribe via MediatR and run independently | Proven by cross-module delivery test (Notifications handler fires on Identity event); independence proven by `IsolatedNotificationPublisher`. Gamification handlers will subscribe when P4-02+ is built. | Gate 4 + api-tester |
| A failing handler does not block other handlers or the originating request | `IsolatedNotificationPublisher` catches per-handler; failing-handler test (AC #3); api-tester confirms HTTP 2xx even when one handler throws. | Gate 4 + api-tester |
| Event handling suitable for background/event-driven processing (NFR-2) | `IsolatedNotificationPublisher` runs handlers non-blocking (parallel or fire-and-continue); `IEventDispatcher` seam is in place for Tier-2 Outbox upgrade. | Gate 4 |
| AC-extra: publish exactly once after commit, NOT on rollback (ADR 0001 §4) | Unit test: command that rolls back → zero handler invocations. `UnitOfWorkBehavior` publish step is after `CommitAsync`. | Gate 4 |
| AC-extra: cross-module delivery works | Cross-module test: Identity publishes → Notifications handler receives (proves unified MediatR registration). | Gate 4 |
| AC-extra: Catalog untouched | Reviewer diff: zero changes to any Catalog project file. | Gate 2 + Gate 4 |
| AC-extra: no PII in event payloads | Reviewer confirms `LessonCompletedIntegrationEvent` and `UserRegisteredIntegrationEvent` carry no name/email/DOB fields beyond opaque IDs. | Gate 1 + Gate 3 |

---

## Recommended ADR 0002 Outline (for backend-feature to draft in Batch 1)

```
# ADR 0002 — Domain Events & Dispatch

Status: Accepted
Date: [date]
Related: ADR 0001 (Unit of Work)

## Context
[Platform not event-driven; per-module MediatR isolation; no UoW behavior; Entity<TId> exists but
FullAuditedEntity chain does not extend it; no publisher; cross-module fan-out impossible today.]

## Decision
1. Dispatch mechanism: publish domain events inside UnitOfWorkBehavior after CommitAsync (not
   SaveChangesInterceptor). Collect Entity<TId> domain events from ChangeTracker, call
   IDomainEventDispatcher.DispatchAsync, then ClearDomainEvents().
2. Independent fan-out: IsolatedNotificationPublisher (INotificationPublisher) — sequential-with-
   isolation or parallel; catch + ILoggerManager per handler; one failing handler does not abort others.
3. Cross-module MediatR: all module Application assemblies registered together at Host level.
4. IEventDispatcher seam: publish via IDomainEventDispatcher, not raw IPublisher sprinkled in handlers.
   Outbox (Tier 2) plugs into this seam when durability is required (see ADR 0001 §3).
5. Aggregate base: new modules use AggregateRoot (audit + DomainEvents) from Shared.Kernel.
   Catalog NOT retrofitted.
6. In-process = acceptable for MVP; Outbox deferred; Hangfire/Quartz = O1.3.

## Consequences
[...]
```
