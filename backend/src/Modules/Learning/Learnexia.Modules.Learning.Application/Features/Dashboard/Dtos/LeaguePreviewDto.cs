namespace Learnexia.Modules.Learning.Application.Features.Dashboard.Dtos;

/// <summary>
/// Phase-2 stub. Always null in Phase 2 — populated by P4-07 (leagues engine).
/// Fields are Phase-4 hints only; exact schema may change.
/// </summary>
public record LeaguePreviewDto(
    string? TierName,
    int? Rank,
    int? TotalPlayers,
    int? XpThisWeek         // TODO P4-07
);
