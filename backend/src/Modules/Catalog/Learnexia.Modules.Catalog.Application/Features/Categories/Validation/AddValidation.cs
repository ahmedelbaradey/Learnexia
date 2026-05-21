using FluentValidation;
using Learnexia.Modules.Catalog.Application.Features.Categories.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Catalog.Application.Features.Categories.Validation;

public class AddValidation : AbstractValidator<AddCategoryCommand>
{
    private readonly IStringLocalizer<SharedResources> _localizer;

    public AddValidation(IStringLocalizer<SharedResources> localizer)
    {
        _localizer = localizer;
        Include(new BaseValidation(_localizer));
    }
}
