# ADR 0001 — Unit of Work strategy for backend

- **Status:** Accepted
- **Date:** 2026-05-21
- **Applies to:** [backend](../../../backend/) (.NET 10 modular monolith, EF Core / Npgsql)
- **Related:** [architecture.md §12](../../architecture.md) · [CONVENTIONS.md §8](../CONVENTIONS.md)

## Context

EF Core's `DbContext` is itself a Unit of Work, and it is registered **Scoped** (one per request). However, the Catalog reference [`GenericRepository`](../../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Repository/GenericRepository.cs) calls `SaveChangesAsync(userId)` **inside every write method**, committing per operation. Consequences:

- Multi-entity writes are **not atomic** (partial state can persist if a later write fails).
- The implicit per-request UoW is flushed eagerly and provides no transactional grouping.

This is acceptable for single-entity CRUD (Catalog today) but **not** for the upcoming modules. The first place it bites is **Gamification**: a `LessonCompleted` command must atomically write an `Attempt`, update `StudentSkillMastery`, insert an `XPTransaction`, and possibly award a `StudentBadge` / update a streak.

## Decision

1. **Do not retrofit Catalog.** It is demo scaffolding being replaced by the Learning module; changing it is churn without payoff.

2. **For new modules** (Learning, Gamification, Curriculum, …) adopt **deferred commit**:
   - Repositories **must not** call `SaveChangesAsync`. They only `Add/Update/Remove` on the `DbSet`.
   - A MediatR **`UnitOfWorkBehavior<TRequest,TResponse>`** (constrained to `ICommand<>`, registered after `ValidationBehavior`) opens a transaction, runs the handler, calls the module `DbContext.SaveChangesAsync(currentUserId)` **once**, and commits; it rolls back on exception. Queries never commit.
   - Audit stamping stays in the `DbContext.SaveChangesAsync(int userId)` override (runs at flush time — unchanged).

   ```csharp
   public sealed class UnitOfWorkBehavior<TReq, TRes> : IPipelineBehavior<TReq, TRes>
       where TReq : ICommand<TRes>
   {
       private readonly <Module>DbContext _db;
       private readonly ICurrentUserService _user;
       public UnitOfWorkBehavior(<Module>DbContext db, ICurrentUserService user) { _db = db; _user = user; }

       public async Task<TRes> Handle(TReq req, RequestHandlerDelegate<TRes> next, CancellationToken ct)
       {
           await using var tx = await _db.Database.BeginTransactionAsync(ct);
           var res = await next();                                   // handler stages changes only
           await _db.SaveChangesAsync(_user.UserId.GetValueOrDefault());
           await tx.CommitAsync(ct);
           return res;
       }
   }
   ```

3. **Scope = one module DbContext.** A transaction never spans two modules (separate DbContexts/schemas, no cross-module FKs — by design). Cross-module consistency uses **integration events**; when an event must not be lost, use the **Outbox pattern** (persist the event row in the same transaction, dispatch after commit). **Never** open a shared transaction across modules.

4. **Domain/integration events publish after commit** (or via the outbox), never before — so consumers don't react to uncommitted state.

## Consequences

**Positive:** atomic multi-entity writes within a module; handlers stop managing persistence; consistent with the existing `ValidationBehavior` style.

**Costs / caveats:**
- The Catalog [`BaseService.AddAsync`](../../../backend/src/Modules/Catalog/Learnexia.Modules.Catalog.Infrastructure/Service/BaseService.cs) returns success **based on the immediate save result**. New modules must **not** reuse that save-at-call-time coupling — either refactor `BaseService` to stage-only (the behavior owns success/rollback) or have handlers use repositories directly.
- Behavior ordering matters: `ValidationBehavior` first (reject before opening a transaction), then `UnitOfWorkBehavior`.
- Catalog and new modules will temporarily use **two different commit models** — documented and intentional.
- No cross-module atomicity — accept eventual consistency on the event path (outbox where durability matters).

## Migration path
- New modules implement this from day one.
- If/when Catalog is replaced by the Learning module, the eager-save `GenericRepository` retires with it.
