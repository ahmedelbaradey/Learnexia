using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Subjects.Commands.Reorder;

public class ReorderSubjectsCommandValidator : AbstractValidator<ReorderSubjectsCommand>
{
    // A grade has at most 4 SubjectCodes × 2 languages = 8 live trees, but we allow 12
    // to give comfortable headroom for future expansion without blocking legitimate use.
    private const int MaxSubjectsPerReorder = 12;

    public ReorderSubjectsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.SubjectIds)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .Must(ids => ids.Count <= MaxSubjectsPerReorder)
                .WithMessage(localizer[SharedResourcesKey.ReorderListTooLong]);

        RuleForEach(x => x.SubjectIds)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
