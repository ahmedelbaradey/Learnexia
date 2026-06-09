using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateBadgeDefinition;

/// <summary>Admin command: create a new <c>BadgeDefinition</c> catalog row.</summary>
public sealed record CreateBadgeDefinitionCommand(
    string Code,
    string Name,
    string Description,
    string IconKey,
    BadgeRarity Rarity,
    int SortOrder,
    BadgeTriggerType TriggerType,
    int? Threshold,
    int RewardXp) : ICommand<BaseResponse<int>>;
