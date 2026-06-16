using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Badge evaluator for <see cref="StreakAdvancedDomainEvent"/> (P4-05, AC1).
/// Consumed in-module — the domain event is raised by <c>StudentXpProfile.AdvanceStreak</c>
/// (or <c>ResetStreakAndStart</c>) and dispatched AFTER commit by <c>UnitOfWorkBehavior</c>
/// (ADR 0002 §2).
///
/// Delegates all catalog + earned-set reads and command dispatch to <see cref="IBadgeService"/>
/// so this handler stays repository-free per §7 CONVENTIONS.
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
    private readonly IBadgeService _badgeService;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public StreakAdvancedBadgeHandler(
        IBadgeService badgeService,
        IMediator mediator,
        ILoggerManager logger)
    {
        _badgeService = badgeService;
        _mediator     = mediator;
        _logger       = logger;
    }

    public async Task Handle(StreakAdvancedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _badgeService.EvaluateAndDispatchBadgesAsync(
                triggerType:     BadgeTriggerType.StreakThreshold,
                value:           notification.NewStreak,
                studentId:       notification.StudentId,
                originEventType: nameof(StreakAdvancedDomainEvent),
                originEventId:   notification.EventId,
                awardedAtUtc:    notification.OccurredOnUtc,
                mediator:        _mediator,
                ct:              ct);
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
