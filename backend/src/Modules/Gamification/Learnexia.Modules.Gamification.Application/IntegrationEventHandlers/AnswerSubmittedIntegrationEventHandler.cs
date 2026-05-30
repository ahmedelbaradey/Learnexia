using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AnswerSubmittedIntegrationEvent"/> (produced by the Learning
/// module's <c>SubmitAnswerCommandHandler</c>).
///
/// Pattern A (Q5 lead-decision): sends <see cref="AwardAnswerSubmittedXpCommand"/> via MediatR
/// only when <c>CorrectAnswerCount &gt; 0</c>. Wrong answers are short-circuited here before sending
/// the command (cheaper than a round-trip through the pipeline for a no-op).
///
/// Fail-soft: exceptions are caught, logged, and swallowed — a failed award must not
/// propagate back to the Learning module or block sibling handlers (ADR 0002 §3).
/// </summary>
public sealed class AnswerSubmittedIntegrationEventHandler
    : INotificationHandler<AnswerSubmittedIntegrationEvent>
{
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public AnswerSubmittedIntegrationEventHandler(IMediator mediator, ILoggerManager logger)
    {
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(
        AnswerSubmittedIntegrationEvent notification,
        CancellationToken cancellationToken)
    {
        try
        {
            // Wrong answers earn no XP (FR-GM-1 "correct +10" — no award for incorrect).
            if (notification.CorrectAnswerCount == 0)
            {
                _logger.LogInfo(
                    $"P4-02: AnswerSubmitted with CorrectAnswerCount=0 — no XP awarded " +
                    $"(eventId={notification.EventId}, studentId={notification.StudentId}).");
                return;
            }

            _logger.LogInfo(
                $"P4-02: AnswerSubmittedIntegrationEvent received (correct answer) " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
                $"lessonId={notification.LessonId}).");

            await _mediator.Send(new AwardAnswerSubmittedXpCommand(
                StudentId: notification.StudentId,
                LessonId: notification.LessonId,
                SkillId: notification.SkillId,
                CorrectAnswerCount: notification.CorrectAnswerCount,
                OriginEventId: notification.EventId,
                OccurredAtUtc: notification.OccurredOnUtc),
                cancellationToken);
        }
        catch (Exception ex)
        {
            // Fail-soft: log + swallow.
            _logger.LogError(ex,
                $"P4-02: Error handling AnswerSubmittedIntegrationEvent " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}) — award may be missed.");
        }
    }
}
