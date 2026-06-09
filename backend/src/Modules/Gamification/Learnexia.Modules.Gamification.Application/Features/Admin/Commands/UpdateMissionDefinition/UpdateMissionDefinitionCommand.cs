using Learnexia.Modules.Gamification.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateMissionDefinition;

/// <summary>Admin command: update mutable fields on an existing <c>MissionDefinition</c>. Code is immutable.</summary>
public sealed record UpdateMissionDefinitionCommand(
    int Id,
    string IconKey,
    string TitleKey,
    MissionType Cadence,
    MissionTargetType TargetType,
    int Target,
    int RewardXp,
    int SortOrder) : ICommand<BaseResponse<bool>>;
