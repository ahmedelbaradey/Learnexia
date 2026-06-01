namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Dashboard summary of the student's current weekly league cohort. Populated by P4-07
/// via IStudentLeagueQuery (hand-projection in GetDashboardQueryHandler — no AutoMapper).
/// Null for brand-new students with no profile.
/// </summary>
public record LeaguePreviewDto(
    string? TierName,
    int? Rank,
    int? TotalPlayers,
    int? XpThisWeek
);
