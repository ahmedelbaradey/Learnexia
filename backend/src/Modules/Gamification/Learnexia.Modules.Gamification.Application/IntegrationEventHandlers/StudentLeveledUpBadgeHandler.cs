using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Badge evaluator for <see cref="StudentLeveledUpDomainEvent"/> (P4-05, AC1).
/// Consumed in-module — the event is raised by <c>StudentXpProfile.ApplyAward</c> when a level
/// threshold is crossed, and dispatched AFTER commit by <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
///
/// This handler can also be triggered re-entrantly when <c>profile.RecordBadgeEarned</c> raises
/// a <see cref="StudentLeveledUpDomainEvent"/> (e.g., a Legendary +250 XP bonus pushes the
/// student from level 4 to 5). The <c>alreadyEarned</c> set passed to <c>BadgePredicateEvaluator</c>
/// prevents infinite chains — each badge awards at most once per student.
///
/// Delegates all catalog + earned-set reads and command dispatch to <see cref="IBadgeService"/>
/// so this handler stays repository-free per §7 CONVENTIONS.
///
/// Practice Mode by-construction: <c>AwardLessonCompletedXpCommandHandler</c> and
/// <c>AwardAnswerSubmittedXpCommandHandler</c> both short-circuit in Practice Mode;
/// no XP is awarded; no level-up event fires. No explicit PM gate needed here.
///
/// Fail-soft: outer try/catch ensures a crash in badge evaluation does NOT kill sibling handlers.
///
/// Timestamp: <see cref="StudentLeveledUpDomainEvent.OccurredOnUtc"/> is used as <c>AwardedAtUtc</c>.
/// </summary>
public sealed class StudentLeveledUpBadgeHandler
    : INotificationHandler<StudentLeveledUpDomainEvent>
{
    private readonly IBadgeService _badgeService;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpBadgeHandler(
        IBadgeService badgeService,
        IMediator mediator,
        ILoggerManager logger)
    {
        _badgeService = badgeService;
        _mediator     = mediator;
        _logger       = logger;
    }

    public async Task Handle(StudentLeveledUpDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _badgeService.EvaluateAndDispatchBadgesAsync(
                triggerType:     BadgeTriggerType.LevelThreshold,
                value:           notification.NewLevel,
                studentId:       notification.StudentId,
                originEventType: nameof(StudentLeveledUpDomainEvent),
                originEventId:   notification.EventId,
                awardedAtUtc:    notification.OccurredOnUtc,
                mediator:        _mediator,
                ct:              ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Error in StudentLeveledUpBadgeHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"newLevel={notification.NewLevel}).");
        }
    }
}
