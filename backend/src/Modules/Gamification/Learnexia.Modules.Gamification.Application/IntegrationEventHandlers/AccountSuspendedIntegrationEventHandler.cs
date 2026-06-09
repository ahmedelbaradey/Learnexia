using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AccountSuspendedIntegrationEvent"/> (P7-07 BE-5).
/// Produced by the Identity module's <c>SuspendAccountCommandHandler</c>.
///
/// When a student account is suspended, this handler freezes the student's active
/// streak and league state so no gamification progress is lost or awarded during
/// the suspension period. On reactivation (<see cref="AccountReactivatedIntegrationEventHandler"/>),
/// the freeze is lifted.
///
/// Pattern: mirrors <see cref="LessonCompletedIntegrationEventHandler"/> (P4-02 Q5).
/// Fail-soft: exceptions are caught, logged, and swallowed. A failed freeze must not
/// propagate back to the Identity module's committed status change or block other handlers.
///
/// Stub note (P7-07 wave): the freeze logic is stubbed as a log-only no-op until the
/// Gamification module defines a FreezeStudentGamificationCommand. The stub is wired so
/// future implementation only requires replacing the log call with a mediator dispatch.
/// Carries only opaque identifiers; no PII in this handler.
/// </summary>
public sealed class AccountSuspendedIntegrationEventHandler
    : INotificationHandler<AccountSuspendedIntegrationEvent>
{
    private readonly ILoggerManager _logger;

    public AccountSuspendedIntegrationEventHandler(ILoggerManager logger)
    {
        _logger = logger;
    }

    public Task Handle(
        AccountSuspendedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Stub: log the event. Freeze XP/streak/league state when a dedicated
            // FreezeStudentGamificationCommand is added to the Gamification module.
            _logger.LogInfo(
                $"P7-07: AccountSuspendedIntegrationEvent received " +
                $"(eventId={notification.EventId}, userId={notification.UserId}). " +
                $"Gamification freeze is stubbed — no state changed.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: Error handling AccountSuspendedIntegrationEvent " +
                $"(eventId={notification.EventId}, userId={notification.UserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
