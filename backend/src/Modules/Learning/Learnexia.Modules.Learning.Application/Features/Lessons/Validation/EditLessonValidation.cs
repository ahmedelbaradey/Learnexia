using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Edit;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Validation;

public class EditLessonValidation : AbstractValidator<EditLessonCommand>
{
    public EditLessonValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new LessonBaseValidation(localizer));

        RuleFor(x => x.Id)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        // P7-02: EstimatedMinutes must be non-negative.
        RuleFor(x => x.EstimatedMinutes)
            .GreaterThanOrEqualTo(0).WithMessage(localizer[SharedResourcesKey.EstimatedMinutesMustBeNonNegative]);
    }
}
