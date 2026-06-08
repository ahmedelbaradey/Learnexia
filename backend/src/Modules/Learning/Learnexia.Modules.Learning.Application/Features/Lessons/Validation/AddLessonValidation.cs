using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Lessons.Commands.Add;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Lessons.Validation;

public class AddLessonValidation : AbstractValidator<AddLessonCommand>
{
    public AddLessonValidation(IStringLocalizer<SharedResources> localizer)
    {
        Include(new LessonBaseValidation(localizer));

        // P7-02: EstimatedMinutes must be non-negative.
        RuleFor(x => x.EstimatedMinutes)
            .GreaterThanOrEqualTo(0).WithMessage(localizer[SharedResourcesKey.EstimatedMinutesMustBeNonNegative]);
    }
}
