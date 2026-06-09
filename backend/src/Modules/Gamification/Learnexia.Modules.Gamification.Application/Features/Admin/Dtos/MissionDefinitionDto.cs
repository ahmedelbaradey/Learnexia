using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;

/// <summary>Admin read DTO for <c>MissionDefinition</c>. Includes inactive rows.</summary>
public sealed record MissionDefinitionDto(
    int Id,
    string Code,
    string IconKey,
    string TitleKey,
    MissionType Cadence,
    MissionTargetType TargetType,
    int Target,
    int RewardXp,
    int SortOrder,
    bool IsActive);
