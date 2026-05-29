using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;

/// <summary>
/// Validator for SubmitAnswerCommand. Commands are auto-validated by ValidationBehavior;
/// queries are NOT validated (per CLAUDE.md rule 4).
/// TimeSpentSeconds is bounded to [0, 3600] — an upper ceiling prevents client-supplied timing
/// from inflating per-question stats (security-auditor pre-emption per P2-08 plan Batch 2).
/// </summary>
public class SubmitAnswerValidation : AbstractValidator<SubmitAnswerCommand>
{
    public SubmitAnswerValidation(IStringLocalizer<SharedResources> localizer)
    {
        RuleFor(x => x.AttemptId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.AttemptIdMustBePositive]);

        RuleFor(x => x.QuestionId)
            .GreaterThan(0)
            .WithMessage(localizer[SharedResourcesKey.QuestionIdMustBePositive]);

        RuleFor(x => x.AnswerPayload)
            .NotEmpty()
            .WithMessage(localizer[SharedResourcesKey.AnswerPayloadRequired]);

        RuleFor(x => x.TimeSpentSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage(localizer[SharedResourcesKey.TimeSpentSecondsMustBeNonNegative])
            .LessThanOrEqualTo(3600)
            .WithMessage(localizer[SharedResourcesKey.TimeSpentSecondsExceedsMaximum]);
    }
}
