using Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;
using Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;
using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Workflow service for the Badge aggregate.
/// Owns badge catalog load, profile load/upsert, idempotency, XP boost, domain mutation,
/// StudentBadge and XpAward row staging.
/// Transactions are owned by UnitOfWorkBehavior (commands) or none (queries).
/// </summary>
public interface IBadgeService
{
    /// <summary>
    /// Awards a badge to the student if not already earned.
    /// Applies timed-event XP boost to the definition's RewardXp.
    /// Raises BadgeEarnedDomainEvent (and optionally StudentLeveledUpDomainEvent).
    /// Returns Success(Unit.Value) in all non-error paths.
    /// </summary>
    Task<BaseResponse<Unit>> AwardBadgeAsync(
        AwardBadgeCommand command,
        CancellationToken ct = default);

    /// <summary>
    /// Returns the full badge catalog with earned/locked state for the given student.
    /// READ-ONLY — no staging or commit.
    /// </summary>
    Task<BaseResponse<MyBadgesResponse>> GetMyBadgesAsync(
        int studentId,
        CancellationToken ct = default);

    /// <summary>
    /// Loads badge definitions for the given trigger type, evaluates which ones the student
    /// has not yet earned, and dispatches one <see cref="AwardBadgeCommand"/> per match via
    /// the provided <paramref name="mediator"/>. Used by post-commit domain/integration event
    /// handlers (INotificationHandler implementations) that must stay repository-free.
    /// Fail-soft: each individual dispatch is independently try/caught; evaluation errors are
    /// swallowed with a log entry so sibling handlers are not affected.
    /// </summary>
    Task EvaluateAndDispatchBadgesAsync(
        BadgeTriggerType triggerType,
        int value,
        int studentId,
        string originEventType,
        Guid originEventId,
        DateTime awardedAtUtc,
        IMediator mediator,
        CancellationToken ct = default);
}
