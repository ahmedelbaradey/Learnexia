using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateMissionDefinition;

/// <summary>Admin command: create a new <c>MissionDefinition</c> catalog row.</summary>
public sealed record CreateMissionDefinitionCommand(
    string Code,
    string IconKey,
    string TitleKey,
    MissionType Cadence,
    MissionTargetType TargetType,
    int Target,
    int RewardXp,
    int SortOrder) : ICommand<BaseResponse<int>>;
