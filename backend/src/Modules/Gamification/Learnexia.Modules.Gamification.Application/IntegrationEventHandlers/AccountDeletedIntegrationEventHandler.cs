using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AccountDeletedIntegrationEvent"/> (P7-07 BE-5).
/// Produced by the Identity module's <c>DeleteAccountCommandHandler</c>.
///
/// When a user account (student) is soft-deleted, this handler cleans up or locks the
/// student's XP/streak/league state. <c>AffectedChildIds</c> is non-empty on a cascade
/// parent-delete, allowing a single event to trigger cleanup for multiple students.
///
/// Fail-soft: mirrors <see cref="AccountSuspendedIntegrationEventHandler"/>.
/// Stub note (P7-07 wave): stubbed as log-only. Full XP/badge/streak purge vs. lock semantics
/// depend on Q-B1 (delete data semantics) which is deferred to the GDPR/COPPA purge wave.
/// The stub logs the affected user ids (no PII) for observability.
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
            // Stub: log the event. Full gamification cleanup (XP/streak/league lock or purge)
            // is deferred to the Q-B1 GDPR/COPPA data-removal wave.
            var childCount = notification.AffectedChildIds?.Count ?? 0;
            _logger.LogInfo(
                $"P7-07: AccountDeletedIntegrationEvent received " +
                $"(eventId={notification.EventId}, userId={notification.UserId}, " +
                $"affectedChildIds={childCount}). " +
                $"Gamification cleanup is stubbed — no state changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: Error handling AccountDeletedIntegrationEvent " +
                $"(eventId={notification.EventId}, userId={notification.UserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
