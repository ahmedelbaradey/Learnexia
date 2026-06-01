using Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Events;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.EventHandlers;

/// <summary>
/// Mission progress handler for <see cref="StreakAdvancedDomainEvent"/> (P4-06, AC2).
/// Second sibling handler on this in-module domain event — alongside
/// <c>StreakAdvancedBadgeHandler</c> (badges). The <c>IsolatedNotificationPublisher</c> fans
/// out to both; they are independent failure domains.
///
/// Consumed in-module: the domain event is raised by <c>StudentXpProfile.AdvanceStreak</c>
/// (or <c>ResetStreakAndStart</c>) and dispatched strictly AFTER a successful commit by
/// <c>UnitOfWorkBehavior</c> (ADR 0002 §2).
///
/// Sends one <see cref="IncrementMissionProgressCommand"/> (increment = 1) for all active
/// <see cref="Domain.Enums.MissionTargetType.MaintainStreak"/> missions belonging to the student.
///
/// Semantics:
///   - <c>DAILY_KEEP_STREAK</c> (target=1): completes after the first streak advance of the day.
///   - <c>WEEKLY_5_DAY_STREAK</c> (target=5): accumulates 1 per streak-advancing day across the week.
///
/// OriginEventId: <see cref="StreakAdvancedDomainEvent"/> carries an auto-generated
/// <see cref="StreakAdvancedDomainEvent.EventId"/> on each instance (Guid.NewGuid()). This is
/// suitable as the MissionProgressLog idempotency key for domain events (which are not redelivered
/// under normal circumstances — UnitOfWorkBehavior dispatches them exactly once post-commit).
///
/// Practice Mode by-construction: <c>AdvanceStreakCommandHandler</c> short-circuits in Practice
/// Mode and never calls <c>AdvanceStreak</c>; therefore <see cref="StreakAdvancedDomainEvent"/>
/// is never raised in Practice Mode. This handler never fires in Practice Mode — no explicit gate needed.
///
/// No-op for students with no instantiated missions: the inner command's
/// GetActiveMissionsForStudentByTargetTypeAsync returns empty.
///
/// Fail-soft: outer try/catch ensures a crash here does NOT propagate back to the streak writer
/// or block the sibling badge handler.
/// </summary>
public sealed class StreakAdvancedMissionHandler
    : INotificationHandler<StreakAdvancedDomainEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public StreakAdvancedMissionHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(StreakAdvancedDomainEvent notification, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new IncrementMissionProgressCommand(
                StudentId: notification.StudentId,
                TargetType: MissionTargetType.MaintainStreak,
                IncrementBy: 1,
                OriginEventId: notification.EventId,
                OriginEventType: nameof(StreakAdvancedDomainEvent),
                OccurredAtUtc: notification.OccurredOnUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-06: Error in StreakAdvancedMissionHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"newStreak={notification.NewStreak}).");
        }
    }
}
