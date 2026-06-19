using Learnexia.Modules.Analytics.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.DomainEvents;
using Learnexia.Shared.Kernel.Entities;
using Learnexia.Shared.Kernel.Messaging;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Analytics.Infrastructure.Behaviors;

/// <summary>
/// Per-module Unit-of-Work behavior for the Analytics module (ADR 0001 §2, extended by ADR 0002 §2).
///
/// Scaffolded to make the module future-ready for write commands without revisiting the serialized
/// shared-file edits. The Analytics module in v1 uses append-only direct SaveChangesAsync (not UoW)
/// for event ingestion, so this behavior fires on zero commands initially.
///
/// The critical cross-module guard (see P4-02 Bug 4) ensures this behavior only runs for commands
/// originating from THIS module's Application assembly, never foreign assemblies.
///
/// Copied verbatim from <c>Moderation.Infrastructure.Behaviors.UnitOfWorkBehavior</c>;
/// only the DbContext and AssemblyReference types are swapped.
/// </summary>
public sealed class UnitOfWorkBehavior<TRequest, TResponse> : IPipelineBehavior<TRequest, TResponse>
    where TRequest : ICommand<TResponse>
{
    private readonly AnalyticsDbContext _db;
    private readonly ICurrentUserService _currentUser;
    private readonly IDomainEventDispatcher _dispatcher;

    public UnitOfWorkBehavior(
        AnalyticsDbContext db,
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
        // Cross-module guard: this behavior must only run for commands from THIS module's Application
        // assembly. Without this guard every UoW fires on every command — opening empty transactions
        // on every module's DbContext (P4-02 Bug 4).
        if (typeof(TRequest).Assembly != typeof(Learnexia.Modules.Analytics.Application.AssemblyReference).Assembly)
            return await next();

        await using var transaction = await _db.Database.BeginTransactionAsync(cancellationToken);

        var response = await next();                                   // handler stages changes only

        await _db.SaveChangesAsync(_currentUser.UserId.GetValueOrDefault());
        await transaction.CommitAsync(cancellationToken);              // commit boundary

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
