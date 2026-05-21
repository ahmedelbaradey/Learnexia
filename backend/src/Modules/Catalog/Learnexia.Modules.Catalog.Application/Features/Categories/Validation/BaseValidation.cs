using FluentValidation;
using Learnexia.Modules.Catalog.Application.Features.Categories.Dtos;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Validation;

public class BaseValidation : AbstractValidator<CategoryDto>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public BaseValidation(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        ApplyValidationsRules();
    }

    public void ApplyValidationsRules()
    {
        RuleFor(x => x.Id)
            .NotNull().WithMessage(_localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage(_localizer[SharedResourcesKey.EmptyNameValidation])
            .NotNull().WithMessage(_localizer[SharedResourcesKey.EmptyNameValidation]);
    }
}
