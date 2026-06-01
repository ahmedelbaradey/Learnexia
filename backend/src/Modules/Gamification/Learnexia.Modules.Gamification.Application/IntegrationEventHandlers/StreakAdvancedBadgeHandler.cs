using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Badge evaluator for <see cref="StreakAdvancedDomainEvent"/> (P4-05, AC1).
/// Consumed in-module — the domain event is raised by <c>StudentXpProfile.AdvanceStreak</c>
/// (or <c>ResetStreakAndStart</c>) and dispatched AFTER commit by <c>UnitOfWorkBehavior</c>
/// (ADR 0002 §2).
///
/// Loads all <c>StreakThreshold</c>-type badge definitions + the student's earned set,
/// calls <see cref="BadgePredicateEvaluator.Match"/> with <c>value = notification.NewStreak</c>,
/// and sends one <see cref="AwardBadgeCommand"/> per matched definition.
///
/// Practice Mode by-construction: <c>AdvanceStreakCommandHandler</c> short-circuits in Practice
/// Mode; this event is never raised; no STREAK_* badge fires. No explicit PM gate needed here.
///
/// Fail-soft: outer try/catch ensures a crash in badge evaluation does NOT kill sibling handlers.
///
/// Timestamp: <see cref="StreakAdvancedDomainEvent.OccurredOnUtc"/> is used as <c>AwardedAtUtc</c>.
/// </summary>
public sealed class StreakAdvancedBadgeHandler
    : INotificationHandler<StreakAdvancedDomainEvent>
{
    private readonly IGamificationRepository _repo;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public StreakAdvancedBadgeHandler(
        IGamificationRepository repo,
        IMediator mediator,
        ILoggerManager logger)
    {
        _repo = repo;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(StreakAdvancedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            var definitions = await _repo.GetBadgeDefinitionsByTriggerAsync(BadgeTriggerType.StreakThreshold, ct);
            if (definitions.Count == 0) return;

            var earned = await _repo.GetEarnedBadgeIdsAsync(notification.StudentId, ct);
            var matches = BadgePredicateEvaluator
                .Match(BadgeTriggerType.StreakThreshold, value: notification.NewStreak, definitions, earned)
                .ToList();

            foreach (var def in matches)
            {
                try
                {
                    await _mediator.Send(new AwardBadgeCommand(
                        StudentId: notification.StudentId,
                        BadgeDefinitionId: def.Id,
                        OriginEventId: Guid.NewGuid(),
                        OriginEventType: nameof(StreakAdvancedDomainEvent),
                        AwardedAtUtc: notification.OccurredOnUtc), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P4-05: Error awarding badge {def.Code} for StreakAdvanced " +
                        $"(studentId={notification.StudentId}, newStreak={notification.NewStreak}, " +
                        $"eventId={notification.EventId}).");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Error in StreakAdvancedBadgeHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"newStreak={notification.NewStreak}).");
        }
    }
}
