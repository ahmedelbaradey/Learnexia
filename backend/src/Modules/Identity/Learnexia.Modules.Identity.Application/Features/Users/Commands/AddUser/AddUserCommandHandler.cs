using System.Text;
using AutoMapper;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Constants;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AddUser;

public class AddUserCommandHandler : BaseResponseHandler, ICommandHandler<AddUserCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IMapper _mapper;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserNotificationService _userNotificationService;
    private readonly ILoggerManager _logger;

    public AddUserCommandHandler(
        IIdentityServiceManager identityServiceManager,
        IMapper mapper,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        IUserNotificationService userNotificationService,
        ILoggerManager logger)
    {
        _mapper = mapper;
        _identityServiceManager = identityServiceManager;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _userNotificationService = userNotificationService;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AddUserCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var existingUserByEmail = await _identityServiceManager.UserManagmentService.FindByEmailAsync(request.Email);
            if (existingUserByEmail != null)
                return BadRequest<string>(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);

            var existingUserByUserName = await _identityServiceManager.UserManagmentService.FindByNameAsync(request.UserName);
            if (existingUserByUserName != null)
                return BadRequest<string>(_localizer[SharedResourcesKey.UsernameAlreadyInUse]);

            string? cvFilePath = null;
            string? photoFilePath = null;

            if (request.CVFile != null)
                cvFilePath = request.CVFile;

            if (request.PersonalPhoto != null)
                photoFilePath = request.PersonalPhoto;

            var user = _mapper.Map<User>(request);
            var password = GenerateTemporaryPassword();
          
            user.CountryCode = "+20";
            user.PreferredLanguage = "ar-EG";
            user.PhoneNumber = request.UserName;
            user.PersonalPhotoPath = photoFilePath;
            user.RegistrationMessageIsSent = request.Roles.All(c => c == "Board Member") ? false : true;
            user.RegistrationIsCompleted = false;
            user.CreatedAt = DateTime.Now;
            user.CreatedBy = _currentUserService.UserId;

            var result = await _identityServiceManager.UserManagmentService.CreateAsync(user, password);
            if (!result.Succeeded)
            {
                var errors = string.Join(", ", result.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            var assignedRoles = new List<string>();
            if (request.Roles.Any())
            {
                foreach (var role in request.Roles)
                {
                    var roleExists = await _identityServiceManager.AuthorizationService.IsRoleNameExist(role);
                    if (roleExists)
                    {
                        await _identityServiceManager.UserManagmentService.AddToRoleAsync(user, role);
                        assignedRoles.Add(role);
                    }
                }
            }
            else
            {
                var defaultRoleExists = await _identityServiceManager.AuthorizationService.IsRoleNameExist("USER");
                if (!defaultRoleExists)
                    await _identityServiceManager.AuthorizationService.AddRoleAsync("USER", null!);

                await _identityServiceManager.UserManagmentService.AddToRoleAsync(user, "USER");
                assignedRoles.Add("USER");
            }

            if (request.Roles.Contains(RoleHelper.LegalCouncil) || request.Roles.Contains(RoleHelper.HeadOfRealEstate) || request.Roles.Contains(RoleHelper.FinanceController) || request.Roles.Contains(RoleHelper.ComplianceLegalManagingDirector))
            {
                foreach (var role in request.Roles)
                {
                    var updatedUser = await _identityServiceManager.UserManagmentService.FindActiveUserWithOnlyRoleAsync(role, user.Id);
                    if (updatedUser != null)
                    {
                        updatedUser.IsActive = false;
                        await _identityServiceManager.UserManagmentService.UpdateAsync(updatedUser);
                    }
                }
            }

            if (user.RegistrationMessageIsSent)
                await SendWhatsAppRegistrationNotificationAsync(user, assignedRoles, password);

            return Success<string>(_localizer[SharedResourcesKey.UserAddedSuccessfully]);
        }
        catch (Exception)
        {
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    private async Task SendWhatsAppRegistrationNotificationAsync(User user, List<string> userRoles, string temporaryPassword)
    {
        try
        {
            _logger.LogInfo($"Sending registration notification to user {user.Id}");

            var phoneNumber = user.PhoneNumber;
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarn($"Invalid phone number for user {user.Id}. Notification skipped.");
                return;
            }

            var loginUrl = "https://Learnexia.com/login";

            var response = await _userNotificationService.SendUserRegistrationMessageAsync(
                user.Id,
                phoneNumber,
                user.UserName!,
                loginUrl,
                CancellationToken.None);

            if (response.IsSuccess)
                _logger.LogInfo($"Registration notification sent successfully to user {user.Id}. MessageId: {response.MessageId}");
            else
                _logger.LogWarn($"Registration notification failed for user {user.Id}. Error: {response.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send registration notification to user {user.Id}");
        }
    }

    private string GenerateTemporaryPassword()
    {
        const string upperCase = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        const string lowerCase = "abcdefghijklmnopqrstuvwxyz";
        const string digits = "0123456789";
        const string specialChars = "@$!%*?&";

        var random = new Random();
        var password = new StringBuilder();
        password.Append(upperCase[random.Next(upperCase.Length)]);
        password.Append(lowerCase[random.Next(lowerCase.Length)]);
        password.Append(digits[random.Next(digits.Length)]);
        password.Append(specialChars[random.Next(specialChars.Length)]);

        const string allChars = upperCase + lowerCase + digits + specialChars;
        for (int i = 4; i < 12; i++)
            password.Append(allChars[random.Next(allChars.Length)]);

        var passwordArray = password.ToString().ToCharArray();
        for (int i = passwordArray.Length - 1; i > 0; i--)
        {
            int j = random.Next(i + 1);
            (passwordArray[i], passwordArray[j]) = (passwordArray[j], passwordArray[i]);
        }

        return new string(passwordArray);
    }
}
