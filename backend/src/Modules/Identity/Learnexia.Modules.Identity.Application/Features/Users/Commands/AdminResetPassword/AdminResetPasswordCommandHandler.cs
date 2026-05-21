using System.Text;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.AdminResetPassword;

public class AdminResetPasswordCommandHandler : BaseResponseHandler, ICommandHandler<AdminResetPasswordCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserNotificationService _userNotificationService;
    private readonly ILoggerManager _logger;

    public AdminResetPasswordCommandHandler(
        IIdentityServiceManager identityServiceManager,
        IStringLocalizer<SharedResources> localizer,
        ICurrentUserService currentUserService,
        IUserNotificationService userNotificationService,
        ILoggerManager logger)
    {
        _identityServiceManager = identityServiceManager;
        _localizer = localizer;
        _currentUserService = currentUserService;
        _userNotificationService = userNotificationService;
        _logger = logger;
    }

    public async Task<BaseResponse<string>> Handle(AdminResetPasswordCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            if (!IsUserEligibleForPasswordReset(user))
                return BadRequest<string>(_localizer[SharedResourcesKey.UserNotEligibleForPasswordReset]);

            var temporaryPassword = GenerateTemporaryPassword();

            if (await _identityServiceManager.AuthenticationService.HasPasswordAsync(user))
                await _identityServiceManager.AuthenticationService.RemovePasswordAsync(user);

            var addPasswordResult = await _identityServiceManager.AuthenticationService.AddPasswordAsync(user, temporaryPassword);
            if (!addPasswordResult.Succeeded)
            {
                var errors = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            user.RegistrationIsCompleted = false;
            user.LastFailedLoginAttempt = null;
            user.AccessFailedCount = 0;

            await _identityServiceManager.UserManagmentService.UpdateSecurityStampAsync(user);

            user.UpdatedAt = DateTime.Now;
            user.UpdatedBy = _currentUserService.UserId;

            var updateResult = await _identityServiceManager.UserManagmentService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            await SendPasswordResetNotificationAsync(user, temporaryPassword);

            return Success<string>(_localizer[SharedResourcesKey.UserPasswordResetSuccessfully]);
        }
        catch (Exception)
        {
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    private bool IsUserEligibleForPasswordReset(User user)
        => user.IsActive && user.RegistrationIsCompleted && user.RegistrationMessageIsSent;

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

    private async Task SendPasswordResetNotificationAsync(User user, string temporaryPassword)
    {
        try
        {
            _logger.LogInfo($"Sending password reset notification to user {user.Id}");

            var phoneNumber = user.PhoneNumber;
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarn($"Invalid phone number for user {user.Id}. Notification skipped.");
                return;
            }

            var response = await _userNotificationService.SendPasswordResetMessageAsync(user.Id, phoneNumber, temporaryPassword, CancellationToken.None);
            if (response.IsSuccess)
                _logger.LogInfo($"Password reset notification sent to user {user.Id}. MessageId: {response.MessageId}");
            else
                _logger.LogWarn($"Password reset notification failed for user {user.Id}. Error: {response.ErrorMessage}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send password reset notification to user {user.Id}");
        }
    }
}
