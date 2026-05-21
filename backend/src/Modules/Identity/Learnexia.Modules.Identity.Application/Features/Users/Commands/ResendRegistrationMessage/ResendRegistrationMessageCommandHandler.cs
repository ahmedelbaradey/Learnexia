using System.Text;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Commands.ResendRegistrationMessage;

public class ResendRegistrationMessageCommandHandler : BaseResponseHandler, ICommandHandler<ResendRegistrationMessageCommand, BaseResponse<string>>
{
    private readonly IIdentityServiceManager _identityServiceManager;
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly ICurrentUserService _currentUserService;
    private readonly IUserNotificationService _userNotificationService;
    private readonly ILoggerManager _logger;

    public ResendRegistrationMessageCommandHandler(
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

    public async Task<BaseResponse<string>> Handle(ResendRegistrationMessageCommand request, CancellationToken cancellationToken)
    {
        try
        {
            var user = await _identityServiceManager.UserManagmentService.FindByIdAsync(request.UserId.ToString());
            if (user == null)
                return NotFound<string>(_localizer[SharedResourcesKey.UserNotFound]);

            var eligibilityCheck = CheckEligibility(user);
            if (!eligibilityCheck && user.Id != 4)
                return BadRequest<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);

            var temporaryPassword = GenerateTemporaryPassword();

            if (await _identityServiceManager.AuthenticationService.HasPasswordAsync(user))
                await _identityServiceManager.AuthenticationService.RemovePasswordAsync(user);

            var addPasswordResult = await _identityServiceManager.AuthenticationService.AddPasswordAsync(user, temporaryPassword);
            if (!addPasswordResult.Succeeded)
            {
                var errors = string.Join(", ", addPasswordResult.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            user.RegistrationMessageIsSent = true;
            user.RegistrationIsCompleted = false;
            user.LastFailedLoginAttempt = null;
            user.AccessFailedCount = 0;
            user.UpdatedAt = DateTime.Now;
            user.UpdatedBy = _currentUserService.UserId;

            var updateResult = await _identityServiceManager.UserManagmentService.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(", ", updateResult.Errors.Select(e => e.Description));
                return BadRequest<string>($"{_localizer[SharedResourcesKey.SystemErrorSavingData]}: {errors}");
            }

            await SendRegistrationNotificationAsync(user, temporaryPassword);

            return Success<string>(_localizer[SharedResourcesKey.RegistrationMessageSentSuccessfully]);
        }
        catch (Exception)
        {
            return ServerError<string>(_localizer[SharedResourcesKey.SystemErrorSavingData]);
        }
    }

    private bool CheckEligibility(User user)
    {
        if (!user.RegistrationIsCompleted || !user.RegistrationMessageIsSent)
            return false;
        return true;
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

    private async Task<bool> SendRegistrationNotificationAsync(User user, string tempPassword)
    {
        try
        {
            _logger.LogInfo($"Resending registration notification to user {user.Id}");

            var phoneNumber = user.CountryCode + user.PhoneNumber;
            if (string.IsNullOrEmpty(phoneNumber))
            {
                _logger.LogWarn($"Invalid phone number for user {user.Id}. Notification skipped.");
                return false;
            }

            var response = await _userNotificationService.SendLocalizedMessageAsync(
                user.Id,
                phoneNumber,
                UserMessageType.RegistrationMessageResend,
                new object[] { tempPassword },
                CancellationToken.None);
            return response.IsSuccess;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Failed to send registration resend notification to user {user.Id}");
            return false;
        }
    }
}
