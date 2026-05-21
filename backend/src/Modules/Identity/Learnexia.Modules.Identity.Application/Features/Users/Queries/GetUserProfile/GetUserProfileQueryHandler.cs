using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Users.Dtos;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Storage;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Queries.GetUserProfile;

public class GetUserProfileQueryHandler : BaseResponseHandler, IQueryHandler<GetUserProfileQuery, BaseResponse<UserProfileResponseDto>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IFilePreviewUrlProvider _previewUrlProvider;

    public GetUserProfileQueryHandler(
        IMapper mapper,
        IIdentityServiceManager identityServiceManager,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        IFilePreviewUrlProvider previewUrlProvider)
    {
        _identityServiceManager = identityServiceManager;
        _mapper = mapper;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _previewUrlProvider = previewUrlProvider;
    }

    public async Task<BaseResponse<UserProfileResponseDto>> Handle(GetUserProfileQuery request, CancellationToken cancellationToken)
    {
        try
        {
            var targetUserId = request.UserId ?? _currentUserService.UserId.GetValueOrDefault();
            var currentUserId = _currentUserService.UserId.GetValueOrDefault();

            if (targetUserId != currentUserId)
            {
                var currentUser = await _identityServiceManager.UserManagmentService.FindByIdAsync(currentUserId.ToString());
                if (currentUser == null)
                    return Unauthorized<UserProfileResponseDto>(_localizer[SharedResourcesKey.UserNotFound]);

                var currentUserRoles = await _identityServiceManager.UserManagmentService.GetUserRolesAsync(currentUser);
                if (!currentUserRoles.Contains("admin") && !currentUserRoles.Contains("superadmin"))
                    return Unauthorized<UserProfileResponseDto>(_localizer[SharedResourcesKey.UnauthorizedUserAccess]);
            }

            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(targetUserId.ToString());
            if (user == null)
                return NotFound<UserProfileResponseDto>(_localizer[SharedResourcesKey.UserNotFound]);

            var userRoles = await _identityServiceManager.UserManagmentService.GetUserRolesAsync(user);

            var response = _mapper.Map<UserProfileResponseDto>(user);
            response.LegalCouncilHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.LegalCouncil, user.Id);
            response.FinanceControllerHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.FinanceController, user.Id);
            response.ComplianceLegalManagingDirectorHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.ComplianceLegalManagingDirector, user.Id);
            response.HeadOfRealEstateHasActiveUser = await HasActiveUserInRoleAsync(RoleHelper.HeadOfRealEstate, user.Id);
            response.Roles = userRoles.ToList();

           

            return Success(response);
        }
        catch (Exception)
        {
            return ServerError<UserProfileResponseDto>(_localizer[SharedResourcesKey.SystemErrorRetrievingData]);
        }
    }

    private async Task<bool> HasActiveUserInRoleAsync(string roleName, int userId)
    {
        try
        {
            var usersInRole = await _identityServiceManager.UserManagmentService.GetUsersByRole(roleName);
            return usersInRole.Any(user => user.Id != userId && user.IsActive);
        }
        catch (Exception)
        {
            return false;
        }
    }

    
}
