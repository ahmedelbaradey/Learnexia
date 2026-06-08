using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Units.Commands.Reorder;

public class ReorderUnitsCommandValidator : AbstractValidator<ReorderUnitsCommand>
{
    // A subject can have many units across an entire curriculum tree. 200 is generous enough
    // to accommodate real trees (typically <50 units per subject) while bounding runaway requests.
    private const int MaxUnitsPerReorder = 200;

    public ReorderUnitsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.UnitIds)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .Must(ids => ids.Count <= MaxUnitsPerReorder)
                .WithMessage(localizer[SharedResourcesKey.ReorderListTooLong]);

        RuleForEach(x => x.UnitIds)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
