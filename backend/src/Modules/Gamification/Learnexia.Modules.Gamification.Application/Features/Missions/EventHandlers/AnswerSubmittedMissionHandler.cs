using Learnexia.Modules.Gamification.Application.Features.Missions.Commands.IncrementMissionProgress;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Missions.EventHandlers;

/// <summary>
/// Mission progress handler for <see cref="AnswerSubmittedIntegrationEvent"/> (P4-06, AC2).
/// Second sibling handler on this event — alongside <c>AnswerSubmittedIntegrationEventHandler</c>
/// (XP + hearts). The <c>IsolatedNotificationPublisher</c> fans out to both; independent failure domains.
///
/// Sends one <see cref="IncrementMissionProgressCommand"/> for all active
/// <see cref="Domain.Enums.MissionTargetType.CorrectAnswers"/> missions (daily + weekly)
/// belonging to the student. Increment = <see cref="AnswerSubmittedIntegrationEvent.CorrectAnswerCount"/>.
///
/// Early exit: skipped entirely when <c>CorrectAnswerCount &lt;= 0</c> (wrong answer — no mission
/// progress). Only correct answers advance CorrectAnswers-type missions.
///
/// Practice Mode note: fires even in Practice Mode (D3). Hearts are not checked here; the wrong-answer
/// path is handled above via the CorrectAnswerCount guard.
///
/// No-op for students with no instantiated missions: the inner command's
/// GetActiveMissionsForStudentByTargetTypeAsync returns empty — no-op, no log noise.
///
/// Fail-soft: outer try/catch ensures a crash here does NOT propagate back to the Learning module
/// or block the sibling XP/hearts handler.
/// </summary>
public sealed class AnswerSubmittedMissionHandler
    : INotificationHandler<AnswerSubmittedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public AnswerSubmittedMissionHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(AnswerSubmittedIntegrationEvent notification, CancellationToken ct)
    {
        // Wrong answer — no mission progress. Early exit before any DB access.
        if (notification.CorrectAnswerCount <= 0)
            return;

        try
        {
            await _mediator.Send(new IncrementMissionProgressCommand(
                StudentId: notification.StudentId,
                TargetType: MissionTargetType.CorrectAnswers,
                IncrementBy: notification.CorrectAnswerCount,
                OriginEventId: notification.EventId,
                OriginEventType: nameof(AnswerSubmittedIntegrationEvent),
                OccurredAtUtc: notification.OccurredOnUtc), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-06: Error in AnswerSubmittedMissionHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}).");
        }
    }
}
