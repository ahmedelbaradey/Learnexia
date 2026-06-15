using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateMissionDefinition;

/// <summary>
/// Handles <see cref="CreateMissionDefinitionCommand"/>.
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class CreateMissionDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<CreateMissionDefinitionCommand, BaseResponse<int>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public CreateMissionDefinitionCommandHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<int>> Handle(
        CreateMissionDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        return await _adminService.CreateMissionDefinitionAsync(request, cancellationToken);
    }
}
