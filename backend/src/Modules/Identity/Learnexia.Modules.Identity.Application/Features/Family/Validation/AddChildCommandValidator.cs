using FluentValidation;
using Learnexia.Modules.Identity.Application.Features.Family.Commands.AddChild;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Identity.Application.Features.Family.Validation;

// Shape-only validation (runs via ValidationBehavior for ICommand<> → 422). Intentionally does NOT
// do a FindByEmailAsync existence lookup — the handler owns uniqueness so the duplicate-email
// rejection is a single 400, mirroring LinkChildCommandValidator. Password complexity mirrors the
// configured Identity policy and RegisterParentCommandValidator.
public class AddChildCommandValidator : AbstractValidator<AddChildCommand>
{
    // Matches the configured Identity password policy (Infrastructure/DependencyInjection.cs):
    // RequireDigit, RequireLowercase, RequireUppercase, RequireNonAlphanumeric, RequiredLength = 6.
    private const string PasswordPolicyPattern =
        @"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^a-zA-Z\d]).{6,}$";

    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddChildCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField]);

        RuleFor(x => x.Email)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .EmailAddress().WithMessage(_localizer[SharedResourcesKey.ProfileInvalidEmailFormat]);

        RuleFor(x => x.Password)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.LoginPasswordRequired])
            .Matches(PasswordPolicyPattern).WithMessage(_localizer[SharedResourcesKey.PasswordComplexityError]);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 6).WithMessage(_localizer[SharedResourcesKey.GradeOutOfRange]);

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(_localizer[SharedResourcesKey.InvalidLanguageCode]);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField]);
    }
}
