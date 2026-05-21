using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.Responses;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authorizations.Queries.GetRoleList;

public class GetRoleListQueryHandler : BaseResponseHandler, IQueryHandler<GetRoleListQuery, BaseResponse<List<GetRoleListResponse>>>
{
    private readonly IIdentityServiceManager _service;
    private readonly IMapper _mapper;
    private readonly ILoggerManager _logger;
    private readonly IStringLocalizer<SharedResources> _localizer;

    public GetRoleListQueryHandler(IIdentityServiceManager service, IMapper mapper, ILoggerManager logger, IStringLocalizer<SharedResources> localizer)
    {
        _service = service;
        _mapper = mapper;
        _logger = logger;
        _localizer = localizer;
    }

    public async Task<BaseResponse<List<GetRoleListResponse>>> Handle(GetRoleListQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var roles = await _service.AuthorizationService.GetRoleListAsync();
            var excludedRoles = new List<string> { RoleHelper.Admin, RoleHelper.SuperAdmin, RoleHelper.Basic };
            if (roles == null)
                return NotFound<List<GetRoleListResponse>>(_localizer[SharedResourcesKey.NotFoundRoles]);

            var rolesMapper = _mapper.Map<List<GetRoleListResponse>>(roles);

            // excluded (admin, superadmin, basic) roles as per JDWA-1689 issue
            rolesMapper = rolesMapper.Where(r => !excludedRoles.Contains(r.roleName.ToLower())).ToList();

            foreach (var roleResponse in rolesMapper)
            {
                var originalRoleName = roleResponse.roleName;
                roleResponse.DisplayName = GetLocalizedRoleName(originalRoleName);
            }

            return Success(rolesMapper);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, ex.Message);
            return ServerError<List<GetRoleListResponse>>(ex.Message);
        }
    }

    private string GetLocalizedRoleName(string roleName)
    {
        if (string.IsNullOrEmpty(roleName))
            return roleName;

        return roleName.ToLower() switch
        {
            "fundmanager" => _localizer[SharedResourcesKey.FundManager],
            "legalcouncil" => _localizer[SharedResourcesKey.LegalCouncil],
            "boardsecretary" => _localizer[SharedResourcesKey.BoardSecretary],
            "boardmember" => _localizer[SharedResourcesKey.BoardMember],
            "superadmin" => _localizer[SharedResourcesKey.SuperAdmin],
            "admin" => _localizer[SharedResourcesKey.Admin],
            "basic" => _localizer[SharedResourcesKey.Basic],
            "user" => _localizer[SharedResourcesKey.User],
            "financecontroller" => _localizer[SharedResourcesKey.FinanceController],
            "compliancelegalmanagingdirector" => _localizer[SharedResourcesKey.ComplianceLegalManagingDirector],
            "headofrealestate" => _localizer[SharedResourcesKey.HeadOfRealEstate],
            "associatedfundmanager" => _localizer[SharedResourcesKey.AssociateFundManager],
            _ => roleName,
        };
    }
}
