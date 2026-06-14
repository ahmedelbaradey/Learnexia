using FluentValidation;
using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Resources;

namespace Learnexia.Modules.Ai.Application.Features.Hint.Commands;

/// <summary>
/// Validates <see cref="GetHintCommand"/> via <c>ValidationBehavior</c> (runs for
/// <c>ICommand&lt;T&gt;</c> only — not for queries).
///
/// Rules (per P3-05-BE-1):
/// <list type="bullet">
///   <item><see cref="GetHintCommand.QuestionId"/> must be positive.</item>
///   <item><see cref="GetHintCommand.AttemptId"/> must be positive.</item>
///   <item><see cref="GetHintCommand.Intent"/> must be <see cref="HelperIntent.Hint"/> or
///         <see cref="HelperIntent.WhyWrong"/>.</item>
///   <item><see cref="GetHintCommand.HintLevel"/> if supplied must be 1..<see cref="HintOptions.MaxHintLevels"/>.</item>
///   <item><see cref="GetHintCommand.WrongAnswer"/> is REQUIRED when Intent == WhyWrong (max 500 chars).</item>
/// </list>
/// </summary>
public sealed class GetHintCommandValidator : AbstractValidator<GetHintCommand>
{
    public GetHintCommandValidator(
        IStringLocalizer<SharedResources> localizer,
        IOptions<HintOptions> hintOptions)
    {
        var maxLevels = hintOptions.Value.MaxHintLevels;

        RuleFor(c => c.QuestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.QuestionIdMustBePositive]);

        RuleFor(c => c.AttemptId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.AttemptIdMustBePositive]);

        RuleFor(c => c.Intent)
            .Must(i => i == HelperIntent.Hint || i == HelperIntent.WhyWrong)
            .WithMessage(localizer[SharedResourcesKey.HintIntentInvalid]);

        When(c => c.HintLevel.HasValue, () =>
        {
            RuleFor(c => c.HintLevel!.Value)
                .InclusiveBetween(1, maxLevels)
                .WithMessage(localizer[SharedResourcesKey.HintLevelOutOfRange]);
        });

        When(c => c.Intent == HelperIntent.WhyWrong, () =>
        {
            RuleFor(c => c.WrongAnswer)
                .NotEmpty()
                .WithMessage(localizer[SharedResourcesKey.HintWrongAnswerRequired])
                .MaximumLength(500)
                .WithMessage(localizer[SharedResourcesKey.HintWrongAnswerTooLong]);
        });
    }
}
