namespace Learnexia.Modules.Gamification.Application.Features.Missions.Queries.GetMyMissions;

/// <summary>
/// Response envelope for <c>GET /api/Gamification/Missions/Me</c> (P4-06).
/// Mirrors <see cref="Learnexia.Modules.Gamification.Application.Features.Badges.Queries.GetMyBadges.MyBadgesResponse"/>
/// in shape — wrapped in <c>BaseResponse&lt;MyMissionsResponse&gt;</c> by the handler.
/// </summary>
/// <param name="Daily">All daily mission instances for the current period, ordered by SortOrder ASC.</param>
/// <param name="Weekly">All weekly mission instances for the current period, ordered by SortOrder ASC.</param>
public sealed record MyMissionsResponse(
    IReadOnlyList<MissionStateDto> Daily,
    IReadOnlyList<MissionStateDto> Weekly);
