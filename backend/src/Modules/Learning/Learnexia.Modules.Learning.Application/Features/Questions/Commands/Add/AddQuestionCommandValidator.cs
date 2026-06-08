using FluentValidation;
using Learnexia.Modules.Learning.Application.Features.Questions.Validation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Commands.Add;

/// <summary>
/// Validates <see cref="AddQuestionCommand"/> before the handler runs.
/// Per-type content shape is delegated to <see cref="QuizQuestionTypeValidation"/>.
/// </summary>
public class AddQuestionCommandValidator : AbstractValidator<AddQuestionCommand>
{
    // P7-SEC (DoS guards): bound the jsonb-column fields.
    private const int MaxQuestionTextLength = 4096;
    private const int MaxOptionsLength = 16384;
    private const int MaxCorrectAnswerLength = 4096;

    public AddQuestionCommandValidator(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.LessonId)
            .GreaterThan(0).WithMessage(localizer[SharedResourcesKey.EmptyIdValidation]);

        RuleFor(x => x.QuestionType)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.QuizQuestionTypeInvalid]);

        RuleFor(x => x.QuestionText)
            .NotEmpty().WithMessage(localizer[SharedResourcesKey.QuizQuestionTextRequired])
            .MaximumLength(MaxQuestionTextLength)
                .WithMessage(localizer[SharedResourcesKey.QuizQuestionTextTooLong]);

        RuleFor(x => x.Options)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .MaximumLength(MaxOptionsLength)
                .WithMessage(localizer[SharedResourcesKey.QuizQuestionOptionsTooLong]);

        RuleFor(x => x.CorrectAnswer)
            .NotNull().WithMessage(localizer[SharedResourcesKey.EmptyRequestValidation])
            .MaximumLength(MaxCorrectAnswerLength)
                .WithMessage(localizer[SharedResourcesKey.QuizQuestionCorrectAnswerTooLong]);

        RuleFor(x => x.Difficulty)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.QuizQuestionDifficultyInvalid]);

        RuleFor(x => x.GeneratedBy)
            .IsInEnum().WithMessage(localizer[SharedResourcesKey.QuizQuestionGeneratedByInvalid]);

        // Wire the orphaned per-type shape validator (P2-06 helper; NOT re-implemented here).
        // Uses Custom(...) so Validate is called exactly once per validation run (P7-SEC: Low #4).
        RuleFor(x => x)
            .Custom((cmd, context) =>
            {
                var error = QuizQuestionTypeValidation.Validate(
                    cmd.QuestionType, cmd.Options, cmd.CorrectAnswer, localizer);
                if (error is not null)
                    context.AddFailure(error);
            });
    }
}
