using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Contracts.Parent;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.AdminGetUserFamily;

public class GetUserFamilyQueryHandler : BaseResponseHandler, IQueryHandler<GetUserFamilyQuery, BaseResponse<AdminFamilyDto>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IParentChildQuery _parentChildQuery;
    private readonly IChildAccountService _childAccountService;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetUserFamilyQueryHandler(
        IIdentityServiceManager service,
        IParentChildQuery parentChildQuery,
        IChildAccountService childAccountService,
        ILoggerManager logger,
        IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _parentChildQuery = parentChildQuery;
        _childAccountService = childAccountService;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<AdminFamilyDto>> Handle(GetUserFamilyQuery request, CancellationToken cancellationToken)
    {
        try
        {
            // ── Input guard ──
            if (request.UserId <= 0)
                return NotFound<AdminFamilyDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var user = await _service.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return NotFound<AdminFamilyDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var roles = await _service.UserManagmentService.GetUserRolesAsync(user);
            var primaryRole = roles.FirstOrDefault() ?? string.Empty;

            List<AdminFamilyMemberDto> children = new();
            List<AdminFamilyMemberDto> parents = new();

            // ── Parent → get linked children via IParentChildQuery seam ──
            if (primaryRole.Equals("Parent", StringComparison.OrdinalIgnoreCase))
            {
                var childIds = await _parentChildQuery.GetChildIdsForParentAsync(request.UserId, cancellationToken);
                if (childIds.Count > 0)
                {
                    var profiles = await _childAccountService.GetChildrenAsync(childIds, cancellationToken);
                    // Finding #6 — Email is null for children in the family list view.
                    // Admin can retrieve a child's email via the individual profile endpoint.
                    children = profiles
                        .Select(p => new AdminFamilyMemberDto(p.Id, p.FullName, null, p.Grade))
                        .ToList();
                }
            }
            // ── Child → get linked parents via IParentChildQuery seam ──
            else if (primaryRole.Equals("Student", StringComparison.OrdinalIgnoreCase))
            {
                var parentIds = await _parentChildQuery.GetParentIdsForChildAsync(request.UserId, cancellationToken);
                if (parentIds.Count > 0)
                {
                    foreach (var parentId in parentIds)
                    {
                        var parentUser = await _service.UserManagmentService.FindByIdAsync(parentId.ToString());
                        if (parentUser != null)
                        {
                            parents.Add(new AdminFamilyMemberDto(
                                parentUser.Id,
                                parentUser.FullName,
                                parentUser.Email ?? string.Empty,
                                null));
                        }
                    }
                }
            }

            var dto = new AdminFamilyDto
            {
                UserId = request.UserId,
                UserRole = primaryRole,
                Children = children,
                Parents = parents,
            };

            _logger.LogInfo($"Admin family query for userId={request.UserId}: {children.Count} children, {parents.Count} parents");
            var response = Success(dto);
            response.Message = _localizer[SharedResourcesKey.AdminUserFamilyRetrievedSuccessfully];
            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in GetUserFamilyQueryHandler");
            return ServerError<AdminFamilyDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }
}
