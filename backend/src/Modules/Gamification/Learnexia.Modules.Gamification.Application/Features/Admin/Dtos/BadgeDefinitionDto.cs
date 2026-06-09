using Learnexia.Modules.Gamification.Domain.Enums;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;

/// <summary>Admin read DTO for <c>BadgeDefinition</c>. Includes inactive rows.</summary>
public sealed record BadgeDefinitionDto(
    int Id,
    string Code,
    string Name,
    string Description,
    string IconKey,
    BadgeRarity Rarity,
    int SortOrder,
    BadgeTriggerType TriggerType,
    int? Threshold,
    int RewardXp,
    bool IsActive);
