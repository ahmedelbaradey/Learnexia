using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;

/// <summary>
/// Represents a single badge definition with the student's earned state (P4-05, AC4).
/// Returned by <see cref="GetMyBadgesQueryHandler"/> for the badge collection endpoint.
/// Earned badges have <see cref="IsEarned"/> = true and a non-null <see cref="AwardedAtUtc"/>.
/// Locked badges have <see cref="IsEarned"/> = false and <see cref="AwardedAtUtc"/> = null.
/// </summary>
public sealed record BadgeStateDto(
    string Code,
    string IconKey,
    BadgeRarity Rarity,
    BadgeTriggerType TriggerType,
    int? Threshold,
    int RewardXp,
    bool IsEarned,
    DateTime? AwardedAtUtc);
