using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Modules.Gamification.Application.Features.Admin.Dtos;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Queries.GetAdminBadgeDefinitions;

/// <summary>
/// Handles <see cref="GetAdminBadgeDefinitionsQuery"/>.
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class GetAdminBadgeDefinitionsQueryHandler
    : BaseResponseHandler, IQueryHandler<GetAdminBadgeDefinitionsQuery, BaseResponse<List<BadgeDefinitionDto>>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public GetAdminBadgeDefinitionsQueryHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<List<BadgeDefinitionDto>>> Handle(
        GetAdminBadgeDefinitionsQuery request,
        CancellationToken cancellationToken)
    {
        return await _adminService.GetAdminBadgeDefinitionsAsync(cancellationToken);
    }
}
