using FluentValidation;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Features.Authentications.Commands.RegisterParent;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Authentications.Validation;

public class RegisterParentCommandValidator : AbstractValidator<RegisterParentCommand>
{
    // Mirrors the configured Identity password policy (Infrastructure/DependencyInjection.cs):
    // RequireDigit, RequireLowercase, RequireUppercase, RequireNonAlphanumeric, RequiredLength = 6.
    private const string PasswordPolicyPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$";

    private readonly IStringLocalizer<SharedResources> _localizer;
    private readonly IIdentityServiceManager _identityServiceManager;

    public RegisterParentCommandValidator(
        IStringLocalizer<SharedResources> localizer,
        IIdentityServiceManager identityServiceManager)
    {
        _localizer = localizer;
        _identityServiceManager = identityServiceManager;
        ApplyValidationRules();
    }

    private void ApplyValidationRules()
    {
        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(_localizer[SharedResourcesKey.ProfileInvalidEmailFormat])
            .MustAsync(BeUniqueEmail).WithMessage(_localizer[SharedResourcesKey.ProfileDuplicateEmail]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .Matches(PasswordPolicyPattern).WithMessage(_localizer[SharedResourcesKey.PasswordComplexityError]);
    }

    private async Task<bool> BeUniqueEmail(string email, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(email))
            return true; // NotEmpty/EmailAddress already report the failure; avoid a noisy duplicate error.

        var existing = await _identityServiceManager.UserManagmentService.FindByEmailAsync(email);
        return existing == null;
    }
}
