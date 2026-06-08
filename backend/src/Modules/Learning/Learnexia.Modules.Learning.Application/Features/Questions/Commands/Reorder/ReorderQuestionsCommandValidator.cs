using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Reorder;

/// <summary>
/// Validates <see cref="ReorderQuestionsCommand"/> before the handler runs.
/// Bounds the list size to prevent runaway requests (DoS guard).
/// Requires a valid LessonId anchor — the handler enforces that every QuestionId belongs to it.
/// </summary>
public class ReorderQuestionsCommandValidator : AbstractValidator<ReorderQuestionsCommand>
{
    // A lesson can have many questions; 500 is generous enough to accommodate
    // real lessons (typically <50 questions) while bounding runaway requests.
    // Mirrors the ContentBlock reorder validator bound.
    private const int MaxQuestionsPerReorder = 500;

    public ReorderQuestionsCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.QuestionIds)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .Must(ids => ids.Count <= MaxQuestionsPerReorder)
                .WithMessage(localizer[SharedResourcesKey.ReorderListTooLong]);

        RuleForEach(x => x.QuestionIds)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);
    }
}
