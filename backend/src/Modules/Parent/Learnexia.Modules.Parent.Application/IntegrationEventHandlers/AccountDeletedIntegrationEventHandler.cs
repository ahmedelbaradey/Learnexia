using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Parent.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AccountDeletedIntegrationEvent"/> in the Parent module (P7-07 BE-5).
/// Produced by the Identity module's <c>DeleteAccountCommandHandler</c>.
///
/// When a user account is soft-deleted, this handler updates the Parent module's family
/// linkage view. Per product rule (Q-B4): deleting a parent does NOT automatically delete
/// children (unless CascadeChildren was set — handled in Identity). The Parent module
/// should clean up its ParentStudent link records for the deleted user(s) so family queries
/// no longer surface deleted accounts.
///
/// Pattern: mirrors <see cref="LearningLanguageChangedIntegrationEventHandler"/> in Learning.
/// Fail-soft: exceptions are caught, logged, and swallowed.
/// Stub note (P7-07 wave): stubbed as log-only until a CleanupFamilyLinksCommand is added
/// to the Parent module. The stub is wired so future implementation only replaces the log.
/// Carries only opaque identifiers; no PII logged.
/// </summary>
public sealed class AccountDeletedIntegrationEventHandler
    : INotificationHandler<AccountDeletedIntegrationEvent>
{
    private readonly ILoggerManager _logger;

    public AccountDeletedIntegrationEventHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task Handle(
        AccountDeletedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Stub: log the event. ParentStudent link cleanup is deferred to the next iteration
            // of this handler. When a CleanupFamilyLinksCommand exists in Parent.Application,
            // replace this log with a _mediator.Send(new CleanupFamilyLinksCommand(notification.UserId)).
            var childCount = notification.AffectedChildIds?.Count ?? 0;
            _logger.LogInfo(
                $"P7-07: AccountDeletedIntegrationEvent received in Parent module " +
                $"(eventId={notification.EventId}, userId={notification.UserId}, " +
                $"affectedChildIds={childCount}). " +
                $"Family link cleanup is stubbed — no state changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: Error handling AccountDeletedIntegrationEvent in Parent module " +
                $"(eventId={notification.EventId}, userId={notification.UserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
