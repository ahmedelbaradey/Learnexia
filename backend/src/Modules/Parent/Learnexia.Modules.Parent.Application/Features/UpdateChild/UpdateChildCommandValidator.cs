using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Parent.Application.Features.UpdateChild;

// Shape-only validation. Email/login/password/role are NOT on this command (no mass-assignment). The
// family-scope link check is enforced in the handler.
public class UpdateChildCommandValidator : AbstractValidator<UpdateChildCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public UpdateChildCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;

        RuleFor(x => x.ChildId)
            .GreaterThan(0).WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField]);

        RuleFor(x => x.FullName)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .MaximumLength(100).WithMessage(_localizer[SharedResourcesKey.ProfileFullNameTooLong]);

        RuleFor(x => x.Grade)
            .InclusiveBetween(1, 6).WithMessage(_localizer[SharedResourcesKey.GradeOutOfRange]);

        RuleFor(x => x.Language)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .Must(lang => lang == "ar" || lang == "en")
            .WithMessage(_localizer[SharedResourcesKey.InvalidLanguageCode]);

        RuleFor(x => x.Country)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.ProfileRequiredField])
            .MaximumLength(100).WithMessage(_localizer[SharedResourcesKey.ProfileCountryTooLong]);
    }
}
