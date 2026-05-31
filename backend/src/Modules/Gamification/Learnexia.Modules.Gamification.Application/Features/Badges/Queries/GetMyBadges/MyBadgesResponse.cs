namespace Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges;

/// <summary>
/// Response envelope for <see cref="GetMyBadgesQuery"/> — wraps the full badge catalog
/// with each entry's earned state for the currently authenticated student (P4-05, AC4).
/// Ordered by Rarity ASC, SortOrder ASC — stable order for FE rendering.
/// </summary>
public sealed record MyBadgesResponse(IReadOnlyList<BadgeStateDto> Badges);
