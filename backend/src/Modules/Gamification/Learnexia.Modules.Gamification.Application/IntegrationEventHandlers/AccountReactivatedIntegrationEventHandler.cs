using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AccountReactivatedIntegrationEvent"/> (P7-07 BE-5).
/// Produced by the Identity module's <c>ReactivateAccountCommandHandler</c>.
///
/// When a suspended student account is reactivated, this handler unfreezes the student's
/// active streak and league state so gamification resumes from where it was suspended.
///
/// Fail-soft: mirrors <see cref="AccountSuspendedIntegrationEventHandler"/>.
/// Stub note (P7-07 wave): stubbed as log-only until UnfreezeStudentGamificationCommand exists.
/// </summary>
public sealed class AccountReactivatedIntegrationEventHandler
    : INotificationHandler<AccountReactivatedIntegrationEvent>
{
    private readonly ILoggerManager _logger;

    public AccountReactivatedIntegrationEventHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task Handle(
        AccountReactivatedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Stub: log the event. Unfreeze XP/streak/league state when a dedicated
            // UnfreezeStudentGamificationCommand is added to the Gamification module.
            _logger.LogInfo(
                $"P7-07: AccountReactivatedIntegrationEvent received " +
                $"(eventId={notification.EventId}, userId={notification.UserId}). " +
                $"Gamification unfreeze is stubbed — no state changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: Error handling AccountReactivatedIntegrationEvent " +
                $"(eventId={notification.EventId}, userId={notification.UserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
