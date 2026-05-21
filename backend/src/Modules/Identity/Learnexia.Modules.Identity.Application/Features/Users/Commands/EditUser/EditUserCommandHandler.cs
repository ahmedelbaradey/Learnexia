using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.EditUser;

public class EditUserCommandHandler : BaseResponseHandler, ICommandHandler<EditUserCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly ILoggerManager _logger;

    public EditUserCommandHandler(
        IIdentityServiceManager identityServiceManager,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        ILoggerManager logger)
    {
        _mapper = mapper;
        _identityServiceManager = identityServiceManager;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(EditUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUser = await _identityServiceManager.UserManagmentService.FindByIdWithRolesAsync(request.Id.ToString());
            if (existingUser == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            var currentRoles = existingUser.Roles.Select(c => c.Name!).ToList();

            var userWithEmail = await _identityServiceManager.UserManagmentService.FindByEmailAsync(request.Email);
            if (userWithEmail != null && userWithEmail.Id != request.Id)
                return BadRequest<string>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);

            var userWithUsername = await _identityServiceManager.UserManagmentService.FindByNameAsync(existingUser.UserName!);
            if (userWithUsername != null && userWithUsername.Id != request.Id)
                return BadRequest<string>(_localizer[SharedResourcesKey.UsernameAlreadyInUse]);

            

            if (!string.IsNullOrWhiteSpace(request.PersonalPhoto))
                existingUser.PersonalPhotoPath = request.PersonalPhoto;

            request.UserName = existingUser.UserName!;
            _mapper.Map(request, existingUser);

            existingUser.UpdatedAt = DateTime.Now;
            existingUser.UpdatedBy = _currentUserService.UserId;

            var singleHolderRoles = new[] { RoleHelper.LegalCouncil, RoleHelper.FinanceController, RoleHelper.ComplianceLegalManagingDirector, RoleHelper.HeadOfRealEstate };
            var newRole = request.Roles.FirstOrDefault();
            if (singleHolderRoles.Contains(newRole))
            {
                var conflictingUser = await _identityServiceManager.UserManagmentService.FindActiveUserWithOnlyRoleAsync(newRole!, existingUser.Id);
                if (conflictingUser != null)
                {
                    conflictingUser.IsActive = false;
                    conflictingUser.UpdatedAt = DateTime.Now;
                    conflictingUser.UpdatedBy = _currentUserService.UserId;
                    existingUser.IsActive = true;
                    var deactivateResult = await _identityServiceManager.UserManagmentService.UpdateAsync(conflictingUser);
                    if (!deactivateResult.Succeeded)
                    {
                        _logger.LogError(null, $"Failed to deactivate conflicting user {conflictingUser.Id} for role {newRole}");
                        return BadRequest<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
                    }
                    _logger.LogInfo($"Deactivated user {conflictingUser.Id} due to role replacement for {newRole}");
                }
            }

            var result = await _identityServiceManager.UserManagmentService.UpdateAsync(existingUser);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            await _identityServiceManager.AuthorizationService.EditUserRoles(request.Roles, existingUser);

            await HandleConditionalRegistrationAsync(existingUser, currentRoles, request.Roles);
            HandleRoleChangeNotifications(existingUser, currentRoles, request.Roles);

            return Success<string>(_localizer[SharedResourcesKey.UserUpdatedSuccessfully]);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error editing user {request.Id}");
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    private async Task HandleConditionalRegistrationAsync(User user, IList<string> currentRoles, List<string> newRoles)
    {
        try
        {
            var hadOnlyBoardMemberRole = currentRoles.Count == 1 && currentRoles.Contains(RoleHelper.BoardMember);
            var gettingFundManagerRole = newRoles.Contains(RoleHelper.FundManager) && !currentRoles.Contains(RoleHelper.FundManager);
            var gettingAssociateFundManagerRole = newRoles.Contains(RoleHelper.AssociateFundManager) && !currentRoles.Contains(RoleHelper.AssociateFundManager);
            var registrationMessageNotSent = !user.RegistrationMessageIsSent;

            if (hadOnlyBoardMemberRole && (gettingFundManagerRole || gettingAssociateFundManagerRole) && registrationMessageNotSent)
            {
                user.RegistrationMessageIsSent = true;
                user.UpdatedAt = DateTime.Now;
                user.UpdatedBy = _currentUserService.UserId;
                await _identityServiceManager.UserManagmentService.UpdateAsync(user);
                _logger.LogInfo($"Registration message flag set for user {user.Id}");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling conditional registration for user {user.Id}");
        }
    }

    // NOTE: backend/ built Notification entities here but never persisted them (dead code). Kept as
    // logging only to preserve module isolation (no reference to the Notifications module).
    private void HandleRoleChangeNotifications(User user, IList<string> currentRoles, List<string> newRoles)
    {
        try
        {
            var fundManagerRemoved = currentRoles.Contains(RoleHelper.FundManager) && !newRoles.Contains(RoleHelper.FundManager);
            var associateFundManagerRemoved = currentRoles.Contains(RoleHelper.AssociateFundManager) && !newRoles.Contains(RoleHelper.AssociateFundManager);

            if (fundManagerRemoved)
                _logger.LogInfo($"Relieve of duties notification for user {user.Id}, role: Fund Manager");

            if (associateFundManagerRemoved)
                _logger.LogInfo($"Relieve of duties notification for user {user.Id}, role: Associate Fund Manager");

            var singleHolderRoles = new[] { RoleHelper.LegalCouncil, RoleHelper.FinanceController, RoleHelper.ComplianceLegalManagingDirector, RoleHelper.HeadOfRealEstate };
            var currentSingleHolderRole = currentRoles.FirstOrDefault(r => singleHolderRoles.Contains(r));
            var newSingleHolderRole = newRoles.FirstOrDefault(r => singleHolderRoles.Contains(r));

            if (currentSingleHolderRole != newSingleHolderRole && (currentSingleHolderRole != null || newSingleHolderRole != null))
                _logger.LogInfo($"Role update notification for user {user.Id}, from: {currentSingleHolderRole} to: {newSingleHolderRole}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error handling role change notifications for user {user.Id}");
        }
    }
}
