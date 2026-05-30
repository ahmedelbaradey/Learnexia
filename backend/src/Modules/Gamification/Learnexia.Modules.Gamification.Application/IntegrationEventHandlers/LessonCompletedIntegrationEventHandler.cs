using Learnexia.Modules.Gamification.Application.Features.Streaks.Commands.AdvanceStreak;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardLessonCompletedXp;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="LessonCompletedIntegrationEvent"/> (produced by the Learning
/// module's <c>CompleteAttemptCommandHandler</c>).
///
/// Pattern A (Q5 lead-decision): sends an internal <see cref="AwardLessonCompletedXpCommand"/> via
/// MediatR so the write runs through Gamification's <c>UnitOfWorkBehavior</c> — clean commit boundary,
/// audit stamping, after-commit domain event dispatch. Does NOT write to <c>GamificationDbContext</c>
/// directly (that would bypass the UoW and break the <c>StudentLeveledUpDomainEvent</c> dispatch path).
///
/// Fail-soft: exceptions are caught, logged, and swallowed. A failed award must not crash the
/// originator's transaction or prevent other notification handlers from running. The
/// <c>IsolatedNotificationPublisher</c> also isolates handler exceptions at the publisher level,
/// but we log here as well for observability (ADR 0002 §3).
///
/// Ghost-event-on-rollback risk (R3, ADR 0002 §3): if the Learning UoW rolls back after publishing
/// this event, XP will already have been awarded for an attempt that never committed. Accepted for
/// MVP; the <c>OriginEventId</c> idempotency key prevents double-award on redelivery.
/// </summary>
public sealed class LessonCompletedIntegrationEventHandler
    : INotificationHandler<LessonCompletedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public LessonCompletedIntegrationEventHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        LessonCompletedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            _logger.LogInfo(
                $"P4-02: LessonCompletedIntegrationEvent received " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"lessonId={notification.LessonId}).");

            await _mediator.Send(new AwardLessonCompletedXpCommand(
                StudentId: notification.StudentId,
                LessonId: notification.LessonId,
                SkillId: notification.SkillId,
                AccuracyPercentage: notification.AccuracyPercentage,
                OriginEventId: notification.EventId,
                OccurredAtUtc: notification.OccurredOnUtc),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-soft: log + swallow. A failed XP award must not propagate back to the Learning
            // module's transaction or block other notification handlers. The IsolatedNotificationPublisher
            // already isolates at the publisher level; this catch is belt-and-suspenders for observability.
            _logger.LogError(ex,
                $"P4-02: Error handling LessonCompletedIntegrationEvent " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}) — award may be missed.");
        }

        // Streak advance (P4-03, D6): separate try/catch — a streak crash must NOT roll back the
        // already-committed XP award. Each _mediator.Send opens its own UnitOfWorkBehavior transaction.
        try
        {
            await _mediator.Send(new AdvanceStreakCommand(
                StudentId: notification.StudentId,
                OriginEventId: notification.EventId,
                OccurredAtUtc: notification.OccurredOnUtc,
                LessonId: notification.LessonId,
                SkillId: notification.SkillId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-03: Error advancing streak " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}) " +
                $"— streak may be missed; XP award already succeeded.");
        }
    }
}
