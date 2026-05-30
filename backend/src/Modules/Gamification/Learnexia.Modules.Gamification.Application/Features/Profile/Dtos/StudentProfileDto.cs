namespace Learnexia.Modules.Gamification.Application.Features.Profile.Dtos;

/// <summary>
/// XP profile snapshot returned by <c>GET /api/Gamification/Profile</c>.
/// All level math is pre-computed by the query handler via <see cref="Domain.Services.LevelCurve"/>.
/// </summary>
public record StudentProfileDto(
    int TotalXp,
    int CurrentLevel,
    int XpIntoCurrentLevel,
    int XpToNextLevel);
