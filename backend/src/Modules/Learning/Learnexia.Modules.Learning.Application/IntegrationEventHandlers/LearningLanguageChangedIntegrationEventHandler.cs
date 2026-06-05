using Learnexia.Modules.Learning.Application.Features.Progress.Commands.ResetMathScienceProgress;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Learning.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="LearningLanguageChangedIntegrationEvent"/> (produced by the
/// Identity module's <c>IdentityChildAccountService.ChangeLearningLanguageAsync</c> after the
/// <c>User.LearningLanguage</c> row has been committed).
///
/// Pattern: mirrors <c>LessonCompletedIntegrationEventHandler</c> in Gamification (P4-02 Q5).
/// Dispatches an internal <see cref="ResetMathScienceProgressCommand"/> via MediatR so the
/// delete runs through Learning's <c>UnitOfWorkBehavior</c> — clean commit boundary, audit
/// stamping, correct deferred-commit semantics (ADR 0001/0002).
///
/// Fail-soft: exceptions are caught, logged, and swallowed. A failed reset must not propagate
/// back to the originating Identity transaction or block other notification handlers.
/// The <c>IsolatedNotificationPublisher</c> already isolates handler exceptions at the publisher
/// level; this catch is belt-and-suspenders for observability.
///
/// Idempotency: <see cref="ResetMathScienceProgressCommandHandler"/> deletes all Math/Science
/// Attempt rows for the student; re-delivery when the set is already empty is naturally a no-op.
/// </summary>
public sealed class LearningLanguageChangedIntegrationEventHandler
    : INotificationHandler<LearningLanguageChangedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public LearningLanguageChangedIntegrationEventHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        LearningLanguageChangedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInfo(
                $"P8-04: LearningLanguageChangedIntegrationEvent received " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"{notification.OldLanguage} → {notification.NewLanguage}).");

            await _mediator.Send(
                new ResetMathScienceProgressCommand(
                    StudentId: notification.StudentId,
                    OriginEventId: notification.EventId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-soft: log + swallow. A failed reset must not propagate back to the Identity
            // module's committed change or block other notification handlers.
            // IsolatedNotificationPublisher already isolates at publisher level; this catch is
            // belt-and-suspenders for observability (ADR 0002 §3).
            _logger.LogError(ex,
                $"P8-04: Error handling LearningLanguageChangedIntegrationEvent " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}) " +
                $"— Math/Science progress reset may be missed.");
        }
    }
}
