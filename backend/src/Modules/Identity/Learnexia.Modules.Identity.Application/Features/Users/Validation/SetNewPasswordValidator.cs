using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.SetNewPassword;
using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class SetNewPasswordValidator : AbstractValidator<SetNewPasswordCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly UserManager<User> _userManager;

    public SetNewPasswordValidator(IStringLocalizer<SharedResources> localizer, UserManager<User> userManager)
    {
        _localizer = localizer;
        _userManager = userManager;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
    }

    private async Task<bool> NewPasswordDifferentFromCurrent(SetNewPasswordCommand command)
    {
        try
        {
            if (string.IsNullOrEmpty(command.NewPassword))
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
