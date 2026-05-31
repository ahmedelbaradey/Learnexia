using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using MediatR;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Commands.AwardBadge;

/// <summary>
/// Awards a badge to a student for a matched predicate condition (P4-05, FR-GM-4).
/// Sent by the three notification handlers (<c>LessonCompletedBadgeHandler</c>,
/// <c>StreakAdvancedBadgeHandler</c>, <c>StudentLeveledUpBadgeHandler</c>) after
/// <c>BadgePredicateEvaluator.Match</c> returns a matching definition.
///
/// Idempotency: two-layer defense —
///   (a) <see cref="Abstractions.IGamificationRepository.HasBadgeAsync"/> pre-check,
///   (b) DB unique index <c>UX_StudentBadges_StudentXpProfileId_BadgeDefinitionId</c> catches races.
///
/// Runs through <c>UnitOfWorkBehavior</c> for an atomic profile update + StudentBadge insert
/// + XpAward insert + BadgeEarnedDomainEvent dispatch after commit.
/// </summary>
public sealed record AwardBadgeCommand(
    int StudentId,
    int BadgeDefinitionId,
    Guid OriginEventId,
    string OriginEventType,
    DateTime AwardedAtUtc) : ICommand<BaseResponse<Unit>>;
