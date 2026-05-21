using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Queries.Responses;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.CheckRoleAvailability;

public class CheckRoleAvailabilityQueryHandler : BaseResponseHandler, IQueryHandler<CheckRoleAvailabilityQuery, BaseResponse<SingleHolderRoleAvailabilityResponse>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ILoggerManager _logger;

    public CheckRoleAvailabilityQueryHandler(
        IIdentityServiceManager identityServiceManager,
        IStringLocalizer<SharedResources> localizer,
        ILoggerManager logger)
    {
        _identityServiceManager = identityServiceManager;
        _localizer = localizer;
        _logger = logger;
    }

    public async Task<BaseResponse<SingleHolderRoleAvailabilityResponse>> Handle(CheckRoleAvailabilityQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var response = new SingleHolderRoleAvailabilityResponse
            {
                LegalCouncilHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.LegalCouncil),
                FinanceControllerHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.FinanceController),
                ComplianceLegalManagingDirectorHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.ComplianceLegalManagingDirector),
                HeadOfRealEstateHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.HeadOfRealEstate),
            };
            return Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking single-holder role availability");
            return ServerError<SingleHolderRoleAvailabilityResponse>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private async Task<bool> HasActiveUserInRoleAsync(string roleName)
    {
        try
        {
            var usersInRole = await _identityServiceManager.UserManagmentService.GetUsersByRole(roleName);
            var hasActiveUser = usersInRole.Any(user => user.IsActive);
            _logger.LogInfo($"Role '{roleName}' has active user: {hasActiveUser}");
            return hasActiveUser;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error checking active users for role '{roleName}'");
            return false;
        }
    }
}
