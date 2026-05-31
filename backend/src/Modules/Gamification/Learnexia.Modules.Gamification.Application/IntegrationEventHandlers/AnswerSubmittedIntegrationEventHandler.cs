using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.LoseHeart;
using Learnexia.Modules.Gamification.Application.Features.Hearts.Commands.RegainHeartFromPractice;
using Learnexia.Modules.Gamification.Application.Features.Xp.Commands.AwardAnswerSubmittedXp;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Cross-module consumer of <see cref="AnswerSubmittedIntegrationEvent"/> (produced by the Learning
/// module's <c>SubmitAnswerCommandHandler</c>).
///
/// Fan-out pattern (ADR 0002 §3):
///   Wrong answer  → <see cref="LoseHeartCommand"/> (P4-04).
///   Correct answer → <see cref="AwardAnswerSubmittedXpCommand"/> (P4-02) THEN
///                    <see cref="RegainHeartFromPracticeCommand"/> (P4-04 — regen when in Practice Mode;
///                    handler no-ops when Hearts &gt; 0).
///
/// Each Send is wrapped in its own try/catch — independent failure domains per ADR 0002 §3.
/// A failed XP award does not roll back a successful heart regeneration, and vice versa.
///
/// Fail-soft: exceptions are caught, logged, and swallowed — a failure must not propagate back
/// to the Learning module or block sibling handlers.
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
        // ── Wrong answer → lose a heart (P4-04) ──────────────────────────────────────────────
        if (notification.CorrectAnswerCount == 0)
        {
            try
            {
                _logger.LogInfo(
                    $"P4-04: AnswerSubmitted wrong answer — sending LoseHeartCommand " +
                    $"(eventId={notification.EventId}, studentId={notification.StudentId}).");

                await _mediator.Send(new LoseHeartCommand(
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
                    $"P4-04: Error losing heart (eventId={notification.EventId}, " +
                    $"studentId={notification.StudentId}) — heart may be missed.");
            }

            return; // Wrong answer never earns XP.
        }

        // ── Correct answer → award XP (P4-02 — unchanged path) ──────────────────────────────
        _logger.LogInfo(
            $"P4-02: AnswerSubmittedIntegrationEvent received (correct answer) " +
            $"(eventId={notification.EventId}, studentId={notification.StudentId}, " +
            $"lessonId={notification.LessonId}).");

        try
        {
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
            _logger.LogError(ex,
                $"P4-02: Error handling AnswerSubmittedIntegrationEvent (XP) " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}) — award may be missed.");
        }

        // ── Correct answer → Practice Mode regen (P4-04) ────────────────────────────────────
        // Sends RegainHeartFromPracticeCommand which no-ops if the student is NOT in Practice Mode
        // (Hearts > 0). This ensures correct answers while at Hearts == 0 restore one heart.
        try
        {
            await _mediator.Send(new RegainHeartFromPracticeCommand(
                StudentId: notification.StudentId,
                OriginEventId: notification.EventId),
                cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-04: Error processing Practice Mode regen (eventId={notification.EventId}, " +
                $"studentId={notification.StudentId}) — regen may be missed.");
        }
    }
}
