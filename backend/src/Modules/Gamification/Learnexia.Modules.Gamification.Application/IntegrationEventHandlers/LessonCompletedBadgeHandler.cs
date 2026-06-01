using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Modules.Gamification.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.IntegrationEventHandlers;

/// <summary>
/// Badge evaluator for <see cref="LessonCompletedIntegrationEvent"/> (P4-05, AC1).
/// Sibling handler alongside <see cref="LessonCompletedIntegrationEventHandler"/> — the
/// <c>IsolatedNotificationPublisher</c> fans out to both; they are independent failure domains.
///
/// Loads all <c>FirstLesson</c>-type badge definitions + the student's earned set,
/// calls <see cref="BadgePredicateEvaluator.Match"/>, and sends one <see cref="AwardBadgeCommand"/>
/// per matched definition. Each send opens its own <c>UnitOfWorkBehavior</c> transaction.
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
    private readonly IGamificationRepository _repo;
    private readonly IMediator _mediator;
    private readonly ILoggerManager _logger;

    public LessonCompletedBadgeHandler(
        IGamificationRepository repo,
        IMediator mediator,
        ILoggerManager logger)
    {
        _repo = repo;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task Handle(LessonCompletedIntegrationEvent notification, CancellationToken ct)
    {
        try
        {
            var definitions = await _repo.GetBadgeDefinitionsByTriggerAsync(BadgeTriggerType.FirstLesson, ct);
            if (definitions.Count == 0) return;

            var earned = await _repo.GetEarnedBadgeIdsAsync(notification.StudentId, ct);
            var matches = BadgePredicateEvaluator
                .Match(BadgeTriggerType.FirstLesson, value: 0, definitions, earned)
                .ToList();

            foreach (var def in matches)
            {
                try
                {
                    await _mediator.Send(new AwardBadgeCommand(
                        StudentId: notification.StudentId,
                        BadgeDefinitionId: def.Id,
                        OriginEventId: Guid.NewGuid(),
                        OriginEventType: nameof(LessonCompletedIntegrationEvent),
                        AwardedAtUtc: notification.OccurredOnUtc), ct);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex,
                        $"P4-05: Error awarding badge {def.Code} for LessonCompleted " +
                        $"(studentId={notification.StudentId}, eventId={notification.EventId}).");
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-05: Error in LessonCompletedBadgeHandler " +
                $"(eventId={notification.EventId}, studentId={notification.StudentId}).");
        }
    }
}
