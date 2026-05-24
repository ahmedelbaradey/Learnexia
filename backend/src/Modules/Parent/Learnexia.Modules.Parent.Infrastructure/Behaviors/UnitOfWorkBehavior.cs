using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;

namespace Learnexia.Modules.Parent.Infrastructure.Behaviors;

/// <summary>
/// Per-module Unit-of-Work behavior for the Parent module (ADR 0001 §2, ADR 0002 §2). Runs ONLY for
/// <see cref="ICommand{TResponse}"/> requests and is registered AFTER <c>ValidationBehavior</c>. Opens a
/// transaction, runs the handler, saves once via <see cref="ParentDbContext"/> (audit stamping), commits,
/// then dispatches any <see cref="IDomainEvent"/>s collected from tracked <see cref="AggregateRoot"/>s.
///
/// Copied verbatim from Learning's UnitOfWorkBehavior; only the DbContext type is swapped.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly ParentDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWorkBehavior(
        ParentDbContext db,
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
