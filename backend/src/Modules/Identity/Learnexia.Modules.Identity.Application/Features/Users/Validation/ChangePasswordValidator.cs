using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ChangePassword;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly UserManager<User> _userManager;

    public ChangePasswordValidator(IStringLocalizer<SharedResources> localizer, UserManager<User> userManager)
    {
        _localizer = localizer;
        _userManager = userManager;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
    }

    private async Task<bool> NewPasswordDifferentFromCurrent(ChangePasswordCommand command)
    {
        try
        {
            if (string.IsNullOrEmpty(command.CurrentPassword) || string.IsNullOrEmpty(command.NewPassword))
                return true;

            var user = await _userManager.FindByIdAsync(command.UserId.ToString());
            if (user == null)
                return true;

            var passwordVerificationResult = _userManager.PasswordHasher.VerifyHashedPassword(user, user.PasswordHash!, command.NewPassword);
            return passwordVerificationResult == PasswordVerificationResult.Failed;
        }
        catch
        {
            return true;
        }
    }
}
