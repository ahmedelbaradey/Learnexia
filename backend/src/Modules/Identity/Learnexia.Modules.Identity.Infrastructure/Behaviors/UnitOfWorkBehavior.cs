using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Identity.Infrastructure.Behaviors;

/// <summary>
/// Per-module Unit-of-Work behavior for the Identity module (ADR 0001 §2, extended by ADR 0002 §2).
///
/// Runs ONLY for <see cref="ICommand{TResponse}"/> requests, and is registered AFTER
/// <c>ValidationBehavior</c> so validation rejects bad input before a transaction is opened. It:
///   1. opens a transaction,
///   2. runs the handler (which stages changes only, in a deferred-commit module),
///   3. saves once via the module <see cref="IdentityModuleDbContext"/> (audit stamping happens here),
///   4. commits,
///   5. ONLY THEN collects <see cref="IDomainEvent"/>s from tracked <see cref="AggregateRoot"/>s and
///      dispatches them via <see cref="IDomainEventDispatcher"/>, then clears them.
///   6. ONLY THEN drains the <see cref="IIdentityDomainEventsBuffer"/> and dispatches each buffered
///      event via the same <see cref="IDomainEventDispatcher"/>. This seam handles domain events raised
///      from <c>User : IdentityUser&lt;int&gt;</c> which cannot extend <c>AggregateRoot</c>
///      (P7-07 Security High #1 fix).
///
/// Domain events are dispatched strictly AFTER a successful commit and NEVER on rollback — so consumers
/// never react to uncommitted state (ADR 0001 §4, ADR 0002 §2).
///
/// Post-commit dispatch failures (steps 5 and 6) are caught + logged per event. They must NOT rethrow:
/// the primary mutation is already committed and rethrowing would 500 a successful delete/suspend.
///
/// Placement note: ADR 0001's skeleton injects the concrete <c>&lt;Module&gt;DbContext</c>, which lives in
/// Infrastructure; the Application layer cannot reference it (Application → Domain only). So this concrete
/// instance lives in Identity.Infrastructure. MediatR resolves pipeline behaviors from DI regardless of
/// the assembly they live in. Each new module follows this same pattern with its own DbContext.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly IdentityModuleDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDomainEventDispatcher _dispatcher;
    private readonly IIdentityDomainEventsBuffer _eventsBuffer;
    private readonly ILoggerManager _logger;

    public UnitOfWorkBehavior(
        IdentityModuleDbContext db,
        ICurrentUserService currentUser,
        IDomainEventDispatcher dispatcher,
        IIdentityDomainEventsBuffer eventsBuffer,
        ILoggerManager logger)
    {
        _db = db;
        _currentUser = currentUser;
        _dispatcher = dispatcher;
        _eventsBuffer = eventsBuffer;
        _logger = logger;
    }

    public async Task<TResponse> Handle(
        TRequest request,
        RequestHandlerDelegate<TResponse> next,
        CancellationToken cancellationToken)
    {
        // Cross-module guard: this behavior must only run for commands from THIS module's Application assembly.
        // All modules register their UoW as open-generic IPipelineBehavior<,> with constraint
        // `TRequest : ICommand<TResponse>`, but ICommand<> lives in Shared.Kernel, so without this guard
        // every UoW fires on every command — opening empty transactions on every module's DbContext and
        // (critically) failing when a command flows through here while its originator's DbContext is in
        // an active transaction. See P4-02 Bug 4.
        if (typeof(TRequest).Assembly != typeof(Learnexia.Modules.Identity.Application.AssemblyReference).Assembly)
            return await next();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var response = await next();                                  // handler stages changes only

        await _db.SaveChangesAsync(_currentUser.UserId.GetValueOrDefault());
        await transaction.CommitAsync(cancellationToken);             // commit boundary

        // ── SUCCESS PATH ONLY ─────────────────────────────────────────────────────────────────────────
        // Everything below runs only after CommitAsync returns without throwing.
        // An exception in next() or CommitAsync bypasses this block entirely; the await-using disposes
        // the transaction (rollback) and the scoped IIdentityDomainEventsBuffer is GC'd — no side-effects.

        // Step 5: AggregateRoot domain events (existing path — for any future AggregateRoot entities).
        var aggregates = _db.ChangeTracker
            .Entries<AggregateRoot>()
            .Where(e => e.Entity.DomainEvents.Count > 0)
            .Select(e => e.Entity)
            .ToList();

        if (aggregates.Count > 0)
        {
            var domainEvents = aggregates.SelectMany(a => a.DomainEvents).ToList();

            foreach (var aggregate in aggregates)
                aggregate.ClearDomainEvents();

            try
            {
                await _dispatcher.DispatchAsync(domainEvents, cancellationToken);
            }
            catch (Exception ex)
            {
                // Post-commit dispatch failure must NOT rethrow (primary mutation committed).
                _logger.LogError(ex,
                    "UnitOfWorkBehavior: post-commit AggregateRoot domain-event dispatch failed — " +
                    "primary mutation is committed; side-effects may be missed.");
            }
        }

        // Step 6: Identity-scoped buffer drain (P7-07 fix — for User : IdentityUser<int> events).
        // Drain() returns an empty list if no events were enqueued (zero-cost for all other commands).
        var bufferedEvents = _eventsBuffer.Drain();
        if (bufferedEvents.Count > 0)
        {
            foreach (var bufferedEvent in bufferedEvents)
            {
                try
                {
                    await _dispatcher.DispatchAsync(new[] { bufferedEvent }, cancellationToken);
                }
                catch (Exception ex)
                {
                    // Per-event isolation: one failing event must not block the others.
                    // Post-commit failure must NOT rethrow (primary mutation committed).
                    _logger.LogError(ex,
                        $"UnitOfWorkBehavior: post-commit Identity buffer dispatch failed for event " +
                        $"{bufferedEvent.GetType().Name} eventId={bufferedEvent.EventId} — " +
                        $"primary mutation is committed; side-effects may be missed.");
                }
            }
        }

        return response;
    }
}
