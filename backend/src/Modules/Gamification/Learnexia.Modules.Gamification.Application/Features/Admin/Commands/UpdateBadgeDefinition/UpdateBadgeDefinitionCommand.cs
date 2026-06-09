using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateBadgeDefinition;

/// <summary>Admin command: update mutable fields on an existing <c>BadgeDefinition</c>. Code is immutable.</summary>
public sealed record UpdateBadgeDefinitionCommand(
    int Id,
    string Name,
    string Description,
    string IconKey,
    BadgeRarity Rarity,
    int SortOrder,
    BadgeTriggerType TriggerType,
    int? Threshold,
    int RewardXp) : ICommand<BaseResponse<bool>>;
