using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Users.Commands.ChangePassword;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Users.Validation;

public class ChangePasswordValidator : AbstractValidator<ChangePasswordCommand>
{
    // Mirrors the configured Identity password policy (Infrastructure/DependencyInjection.cs):
    // RequireDigit, RequireLowercase, RequireUppercase, RequireNonAlphanumeric, RequiredLength = 6.
    private const string PasswordPolicyPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$";

    private readonly IStringLocalizer<SharedResources> _localizer;

    public ChangePasswordValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
        RuleFor(x => x.CurrentPassword)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.PasswordIncorrectCurrent]);

        RuleFor(x => x.NewPassword)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .Matches(PasswordPolicyPattern).WithMessage(_localizer[SharedResourcesKey.PasswordComplexityError])
            .Must(NewPasswordDifferentFromCurrent).WithMessage(_localizer[SharedResourcesKey.PasswordSameAsCurrent]);

        RuleFor(x => x.ConfirmPassword)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .Equal(x => x.NewPassword).WithMessage(_localizer[SharedResourcesKey.PasswordMismatch]);
    }

    // Synchronous same-value check: if NewPassword equals CurrentPassword (string equality), reject early.
    // A deeper hash-based check is not possible here because we do not have the current user context in the
    // validator (UserId is no longer on the command — it comes from JWT in the handler). Identity's own
    // ChangePasswordAsync enforces the hash-level constraint if the strings happen to differ after hashing.
    private static bool NewPasswordDifferentFromCurrent(ChangePasswordCommand command, string newPassword)
        => !string.Equals(command.CurrentPassword, newPassword, StringComparison.Ordinal);
}
