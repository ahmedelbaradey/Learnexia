# ADR 0002 — Domain Events & Dispatch

- **Status:** Accepted (2026-05-21)
- **Date:** 2026-05-21
- **Applies to:** [backend/](../../../backend/) (.NET 10 modular monolith)
- **Related:** [ADR 0001 — Unit of Work](0001-unit-of-work.md) · [architecture.md §4.4 (inter-module)](../../architecture.md) · brief [docs/briefs/P4-01-domain-events-backbone.md](../../briefs/P4-01-domain-events-backbone.md)

## Context

The platform is **not event-driven yet** — only scaffolding exists: the `IDomainEvent`/`IIntegrationEvent : INotification` markers, two unused contract types, and `Entity<TId>`'s domain-event list. **Nothing publishes**, there is no after-commit dispatcher, the one consumer is a `NotImplementedException` stub, and `FullAuditedEntity` (used by Catalog and most aggregates) has **no** domain-event support. Two further blockers were found in code:
- **MediatR is registered per-module-assembly**, so `IPublisher.Publish(...)` would **not** reach handlers in other modules — cross-module fan-out (FR-GM-7) silently fails.
- ADR 0001 commits once per command in `UnitOfWorkBehavior`; events must publish **after** that commit, never on rollback.

Gamification (P4-02+) depends on this: a `LessonCompleted` reaction must fan out to XP/streak/badge handlers. So the event backbone (story **P4-01**) must land first.

## Decision

1. **Aggregate base.** New modules raise domain events via a shared event-capable base (`AggregateRoot` in `Shared.Kernel`, extending the audited entity). **Catalog is not retrofitted** (consistent with ADR 0001).

2. **After-commit domain-event dispatch.** Extend `UnitOfWorkBehavior` (ADR 0001): after the single `SaveChangesAsync` + transaction commit, collect `DomainEvents` from tracked aggregates, dispatch each via an **`IDomainEventDispatcher`**, then clear them. Events are dispatched **only after a successful commit** and **never on rollback**.

3. **Integration events (cross-module).** Continue using `Shared.Contracts` (`IIntegrationEvent : INotification`); publish **after commit**; consumers are `INotificationHandler<T>` in other modules. Define `LessonCompletedIntegrationEvent` now with a **stubbed producer** (Learning module not built); implement the existing `UserRegisteredIntegrationEventHandler` stub.

4. **Unified, isolated publishing.** Register a **single MediatR configuration spanning all module assemblies** (host-level) so `Publish` reaches cross-module handlers, behind an **`IsolatedNotificationPublisher`** that runs handlers independently — one failing handler does **not** abort its siblings (and failures are logged, not swallowed silently).

5. **Reliability seam (Outbox deferred).** Dispatch goes through an **`IEventDispatcher` seam** so a transactional **Outbox** (durable, survives crashes) can be added later (Tier 2) without changing callers. **Outbox is deferred** for P4-01.

6. **Async/scheduled deferred.** **Hangfire/Quartz + `IHostedService`** (league rollover, mission scheduling, hearts reset, report generation, the future outbox dispatch loop) are **out of scope** for P4-01 (Tier 3, later / O1.3).

## Consequences

**Positive:** real, working eventing; atomic + after-commit + cross-module; isolated handler failure; one Shared.Kernel pattern every new module inherits; extensible to an Outbox without caller changes.

**Costs / caveats:**
- **Until the Outbox lands, dispatch is in-process and best-effort** — an event raised by a committed transaction is lost if the process crashes between commit and dispatch. Acceptable for P4-01 (gamification rewards can be recomputed); the Outbox closes this for anything that must be durable.
- Unifying MediatR registration touches **Host wiring** and must preserve each module's handler/validator/behavior scanning.
- `UnitOfWorkBehavior` now does two things (commit + dispatch) — kept cohesive: "complete the unit of work, then publish what it produced."
- Scope stays **per-module DbContext** (ADR 0001); cross-module consistency is via events, never a shared transaction.

## Alternatives considered
- **Raw `SaveChangesInterceptor`** for dispatch — rejected: it fires inside the DbContext save, before the `UnitOfWorkBehavior` transaction commits, making "after-commit" ordering awkward; the behavior already owns the commit boundary.
- **Per-module MediatR only** — rejected: cannot deliver cross-module integration events (the R1 blocker).
- **Message broker (RabbitMQ/MassTransit) now** — rejected: unnecessary for an in-process modular monolith; revisit only if/when services split.
