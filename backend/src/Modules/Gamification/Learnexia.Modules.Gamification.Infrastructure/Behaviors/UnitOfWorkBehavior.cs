using Learnexia.Modules.Gamification.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Gamification.Infrastructure.Behaviors;

/// <summary>
/// Per-module Unit-of-Work behavior for the Gamification module (ADR 0001 §2, ADR 0002 §2).
///
/// Runs ONLY for <see cref="ICommand{TResponse}"/> requests, and is registered AFTER
/// <c>ValidationBehavior</c> so validation rejects bad input before a transaction is opened. It:
///   1. opens a transaction,
///   2. runs the handler (which stages changes only, in a deferred-commit module),
///   3. saves once via the module <see cref="GamificationDbContext"/> (audit stamping happens here),
///   4. commits,
///   5. ONLY THEN collects <see cref="IDomainEvent"/>s from tracked <see cref="AggregateRoot"/>s and
///      dispatches them via <see cref="IDomainEventDispatcher"/>, then clears them.
///
/// Domain events are dispatched strictly AFTER a successful commit and NEVER on rollback — so consumers
/// never react to uncommitted state (ADR 0001 §4, ADR 0002 §2).
///
/// Copied verbatim from Learning's UnitOfWorkBehavior; only the DbContext type is swapped.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly GamificationDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWorkBehavior(
        GamificationDbContext db,
        ICurrentUserService currentUser,
        IDomainEventDispatcher dispatcher)
    {
        _db = db;
        _currentUser = currentUser;
        _dispatcher = dispatcher;
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
        if (typeof(TRequest).Assembly != typeof(Learnexia.Modules.Gamification.Application.AssemblyReference).Assembly)
            return await next();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var response = await next();                                  // handler stages changes only

        await _db.SaveChangesAsync(_currentUser.UserId.GetValueOrDefault());
        await transaction.CommitAsync(cancellationToken);             // commit boundary

        // After a successful commit ONLY: collect, dispatch, then clear domain events.
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

            await _dispatcher.DispatchAsync(domainEvents, cancellationToken);
        }

        return response;
    }
}
