using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Learning.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AccountDeletedIntegrationEvent"/> in the Learning module (P7-07 BE-5).
/// Produced by the Identity module's <c>DeleteAccountCommandHandler</c>.
///
/// When a student account is soft-deleted, this handler determines whether any Learning
/// module state needs to be locked or cleaned up. Per Q-B1 (deferred decision): full
/// data purge (GDPR/COPPA erasure) is deferred to a dedicated purge wave. For now, the
/// Learning module's records for a deleted student remain (preserving history per the
/// product rule), and the student's learning data becomes inaccessible via normal queries
/// once IsActive = false gates the sign-in.
///
/// Fail-soft: mirrors <see cref="LearningLanguageChangedIntegrationEventHandler"/>.
/// Stub note (P7-07 wave): thin no-op log stub. Learning queries key off User.IsActive / JWT,
/// so deleted accounts are effectively locked from the student side without any data mutation here.
/// Full erasure semantics are a separate GDPR/COPPA cleanup story.
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
            // Thin no-op: log and return. Learning history is preserved per the product rule
            // (grade transition / account lifecycle must not purge learning data). Full GDPR/COPPA
            // erasure is deferred to a dedicated wave (Q-B1 decision).
            var childCount = notification.AffectedChildIds?.Count ?? 0;
            _logger.LogInfo(
                $"P7-07: AccountDeletedIntegrationEvent received in Learning module " +
                $"(eventId={notification.EventId}, userId={notification.UserId}, " +
                $"affectedChildIds={childCount}). " +
                $"Learning history preserved — no state changed (GDPR purge deferred per Q-B1).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: Error handling AccountDeletedIntegrationEvent in Learning module " +
                $"(eventId={notification.EventId}, userId={notification.UserId}) — ignored.");
        }

        return Task.CompletedTask;
    }
}
