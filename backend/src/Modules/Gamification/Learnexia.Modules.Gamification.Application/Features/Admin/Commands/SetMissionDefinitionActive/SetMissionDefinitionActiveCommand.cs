using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetMissionDefinitionActive;

/// <summary>Admin command: activate or deactivate a <c>MissionDefinition</c>.</summary>
public sealed record SetMissionDefinitionActiveCommand(
    int Id,
    bool IsActive) : ICommand<BaseResponse<bool>>;
