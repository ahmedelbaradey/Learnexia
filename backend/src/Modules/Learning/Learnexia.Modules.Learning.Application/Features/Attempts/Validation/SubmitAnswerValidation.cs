using System.Text.Json;
using FluentValidation;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Attempts.Commands.SubmitAnswer;

/// <summary>
/// Validator for SubmitAnswerCommand. Commands are auto-validated by ValidationBehavior;
/// queries are NOT validated (per CLAUDE.md rule 4).
/// TimeSpentSeconds is bounded to [0, 3600] — an upper ceiling prevents client-supplied timing
/// from inflating per-question stats (security-auditor pre-emption per P2-08 plan Batch 2).
/// CO-BE-2: a payload that *looks like* a JSON document (starts with '{' or '[') but does not
/// parse is rejected with 422 here — raw scalar payloads (MCQ/TrueFalse/FillInBlank) are
/// untouched, and well-formed-but-wrong-shape JSON still reaches the comparator (graded false).
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
            .WithMessage(localizer[SharedResourcesKey.AnswerPayloadRequired])
            .Must(BeWellFormedJsonWhenStructured)
            .WithMessage(localizer[SharedResourcesKey.AnswerPayloadMalformedJson]);

        RuleFor(x => x.TimeSpentSeconds)
            .GreaterThanOrEqualTo(0)
            .WithMessage(localizer[SharedResourcesKey.TimeSpentSecondsMustBeNonNegative])
            .LessThanOrEqualTo(3600)
            .WithMessage(localizer[SharedResourcesKey.TimeSpentSecondsExceedsMaximum]);
    }

    /// <summary>
    /// Returns false only when the payload starts with '{' or '[' (a structured document — e.g. the
    /// Matching pairs contract) but is not parseable JSON. Null/empty (handled by NotEmpty) and raw
    /// scalar payloads return true so MCQ/TrueFalse/FillInBlank submissions are unaffected.
    /// </summary>
    private static bool BeWellFormedJsonWhenStructured(string? answerPayload)
    {
        if (string.IsNullOrWhiteSpace(answerPayload))
            return true;

        var trimmed = answerPayload.TrimStart();
        if (trimmed[0] != '{' && trimmed[0] != '[')
            return true;

        try
        {
            using var _ = JsonDocument.Parse(answerPayload);
            return true;
        }
        catch (JsonException)
        {
            return false;
        }
    }
}
