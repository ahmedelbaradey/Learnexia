using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.EventHandlers;

/// <summary>
/// Mission progress handler for <see cref="LessonCompletedIntegrationEvent"/> (P4-06, AC2).
/// Third sibling handler on this event — alongside <c>LessonCompletedIntegrationEventHandler</c>
/// (XP + streak) and <c>LessonCompletedBadgeHandler</c> (badges). The
/// <c>IsolatedNotificationPublisher</c> fans out to all three; they are independent failure domains.
///
/// Sends one <see cref="IncrementMissionProgressCommand"/> for all active
/// <see cref="Domain.Enums.MissionTargetType.CompleteLessons"/> missions (daily + weekly)
/// belonging to the student. Each send runs through <c>UnitOfWorkBehavior</c> as one atomic write.
///
/// Practice Mode note: this handler fires even when the student is in Practice Mode (lesson
/// completion is NOT blocked by hearts). D3 decision — no heart check here.
///
/// No-op for students who have never loaded the dashboard: GetActiveMissionsForStudentByTargetTypeAsync
/// returns empty when no StudentMission rows exist for the student yet (lazy instantiation happens
/// on the first dashboard read — D2 decision).
///
/// Fail-soft: the outer try/catch ensures a crash here does NOT propagate back to the Learning
/// module or block the sibling XP/streak/badge handlers.
/// </summary>
public sealed class LessonCompletedMissionHandler
    : INotificationHandler<LessonCompletedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public LessonCompletedMissionHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(LessonCompletedIntegrationEvent notification, CancellationToken ct)
    {
        try
        {
            await _mediator.Send(new IncrementMissionProgressCommand(
                StudentId: notification.StudentId,
                TargetType: MissionTargetType.CompleteLessons,
                IncrementBy: 1,
                OriginEventId: notification.EventId,
                OriginEventType: nameof(LessonCompletedIntegrationEvent),
                OccurredAtUtc: notification.OccurredOnUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-06: Error in LessonCompletedMissionHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}).");
        }
    }
}
