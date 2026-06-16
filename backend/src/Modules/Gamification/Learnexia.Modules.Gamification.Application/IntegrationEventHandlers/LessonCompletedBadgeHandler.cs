using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Badge evaluator for <see cref="LessonCompletedIntegrationEvent"/> (P4-05, AC1).
/// Sibling handler alongside <see cref="LessonCompletedIntegrationEventHandler"/> — the
/// <c>IsolatedNotificationPublisher</c> fans out to both; they are independent failure domains.
///
/// Delegates all catalog + earned-set reads and command dispatch to <see cref="IBadgeService"/>
/// so this handler stays repository-free per §7 CONVENTIONS.
///
/// Practice Mode note: this handler fires even when the student is in Practice Mode
/// (lesson completion is not blocked). The <c>FIRST_LESSON</c> badge correctly awards in
/// Practice Mode. <c>StreakAdvancedDomainEvent</c> and <c>StudentLeveledUpDomainEvent</c>
/// are never raised in Practice Mode, so STREAK_* and LEVEL_* badges cannot fire in Practice
/// Mode by construction (D11).
///
/// Fail-soft: the outer try/catch ensures a crash in badge evaluation does NOT propagate back
/// to the Learning module or block the sibling XP/streak handlers.
/// </summary>
public sealed class LessonCompletedBadgeHandler
    : INotificationHandler<LessonCompletedIntegrationEvent>
{
    private readonly IBadgeService _badgeService;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public LessonCompletedBadgeHandler(
        IBadgeService badgeService,
        IMediator mediator,
        ILoggerManager logger)
    {
        _badgeService = badgeService;
        _mediator     = mediator;
        _logger       = logger;
    }

    public async Task Handle(LessonCompletedIntegrationEvent notification, CancellationToken ct)
    {
        try
        {
            await _badgeService.EvaluateAndDispatchBadgesAsync(
                triggerType:     BadgeTriggerType.FirstLesson,
                value:           0,
                studentId:       notification.StudentId,
                originEventType: nameof(LessonCompletedIntegrationEvent),
                originEventId:   notification.EventId,
                awardedAtUtc:    notification.OccurredOnUtc,
                mediator:        _mediator,
                ct:              ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Error in LessonCompletedBadgeHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}).");
        }
    }
}
