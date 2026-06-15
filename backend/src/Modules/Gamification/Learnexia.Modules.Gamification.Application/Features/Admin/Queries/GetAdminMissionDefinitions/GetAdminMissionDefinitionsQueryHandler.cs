using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminMissionDefinitions;

/// <summary>
/// Handles <see cref="GetAdminMissionDefinitionsQuery"/>.
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class GetAdminMissionDefinitionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminMissionDefinitionsQuery, BaseResponse<List<MissionDefinitionDto>>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public GetAdminMissionDefinitionsQueryHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<List<MissionDefinitionDto>>> Handle(
        GetAdminMissionDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _adminService.GetAdminMissionDefinitionsAsync(cancellationToken);
    }
}
