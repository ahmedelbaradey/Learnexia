using Learnexia.Modules.Gamification.Application.Abstractions;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Features.Admin.Commands.CreateBadgeDefinition;

/// <summary>
/// Handles <see cref="CreateBadgeDefinitionCommand"/>.
/// Delegates to <see cref="IGamificationAdminService"/>. Handler is EF-free and repository-free per §7 CONVENTIONS.
/// </summary>
public sealed class CreateBadgeDefinitionCommandHandler
    : BaseResponseHandler, ICommandHandler<CreateBadgeDefinitionCommand, BaseResponse<int>>
{
    private readonly IGamificationAdminService _adminService;
    private readonly ILoggerManager _logger;

    public CreateBadgeDefinitionCommandHandler(
        IGamificationAdminService adminService,
        ILoggerManager logger)
    {
        _adminService = adminService;
        _logger       = logger;
    }

    public async Task<BaseResponse<int>> Handle(
        CreateBadgeDefinitionCommand request,
        CancellationToken cancellationToken)
    {
        return await _adminService.CreateBadgeDefinitionAsync(request, cancellationToken);
    }
}
