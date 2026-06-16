using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.UpdateMissionDefinition;

/// <summary>
/// Handles <see cref="UpdateMissionDefinitionCommand"/>.
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class UpdateMissionDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<UpdateMissionDefinitionCommand, BaseResponse<bool>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public UpdateMissionDefinitionCommandHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<bool>> Handle(
        UpdateMissionDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        return await _adminService.UpdateMissionDefinitionAsync(request, cancellationToken);
    }
}
