using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.SetBadgeDefinitionActive;

/// <summary>Admin command: activate or deactivate a <c>BadgeDefinition</c>.</summary>
public sealed record SetBadgeDefinitionActiveCommand(
    int Id,
    bool IsActive) : ICommand<BaseResponse<bool>>;
