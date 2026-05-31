using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
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
/// Loads all <c>LevelThreshold</c>-type badge definitions + the student's earned set,
/// calls <see cref="BadgePredicateEvaluator.Match"/> with <c>value = notification.NewLevel</c>,
/// and sends one <see cref="AwardBadgeCommand"/> per matched definition.
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
    private readonly IGamificationRepository _repo;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public StudentLeveledUpBadgeHandler(
        IGamificationRepository repo,
        IMediator mediator,
        ILoggerManager logger)
    {
        _repo = repo;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(StudentLeveledUpDomainEvent notification, CancellationToken ct)
    {
        try
        {
            var definitions = await _repo.GetBadgeDefinitionsByTriggerAsync(BadgeTriggerType.LevelThreshold, ct);
            if (definitions.Count == 0) return;

            var earned = await _repo.GetEarnedBadgeIdsAsync(notification.StudentId, ct);
            var matches = BadgePredicateEvaluator
                .Match(BadgeTriggerType.LevelThreshold, value: notification.NewLevel, definitions, earned)
                .ToList();

            foreach (var def in matches)
            {
                try
                {
                    await _mediator.Send(new AwardBadgeCommand(
                        StudentId: notification.StudentId,
                        BadgeDefinitionId: def.Id,
                        OriginEventId: Guid.NewGuid(),
                        OriginEventType: nameof(StudentLeveledUpDomainEvent),
                        AwardedAtUtc: notification.OccurredOnUtc), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P4-05: Error awarding badge {def.Code} for StudentLeveledUp " +
                        $"(studentId={notification.StudentId}, newLevel={notification.NewLevel}, " +
                        $"eventId={notification.EventId}).");
                }
            }
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
