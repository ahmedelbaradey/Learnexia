using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Reorder;

public class ReorderLessonsCommandValidator : AbstractValidator<ReorderLessonsCommand>
{
    // A unit can have many lessons. 500 is generous for real curricula while bounding runaway requests.
    private const int MaxLessonsPerReorder = 500;

    public ReorderLessonsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonIds)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .Must(ids => ids.Count <= MaxLessonsPerReorder)
                .WithMessage(localizer[SharedResourcesKey.ReorderListTooLong]);

        RuleForEach(x => x.LessonIds)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
