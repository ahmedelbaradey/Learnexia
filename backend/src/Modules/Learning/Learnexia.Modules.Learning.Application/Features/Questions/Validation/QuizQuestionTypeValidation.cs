using System.Text.Json;
using FluentValidation;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;

namespace Learnexia.Modules.Learning.Application.Features.Questions.Validation;

/// <summary>
/// Static helper for per-QuestionType content validation.
/// Validates the Options and CorrectAnswer shape stored as JSON strings.
///
/// Plain static method invoked from a FluentValidation command validator — NOT a Strategy/Factory pattern
/// (CLAUDE.md rule 8: no design pattern without lead approval). Unit-testable via direct method calls.
///
/// Shape rules per type:
///   MCQ        — Options: JSON array of strings (≥2); CorrectAnswer: one of the option strings.
///   TrueFalse  — CorrectAnswer: "true" or "false" (case-insensitive).
///   Matching   — Options: JSON object {"left": [...], "right": [...]} with equal-length arrays.
///   FillInBlank— CorrectAnswer: non-empty string.
/// </summary>
public static class QuizQuestionTypeValidation
{
    /// <summary>
    /// Validates the content shape for a question of the given type.
    /// Returns null when valid; a localized error message string when invalid.
    /// </summary>
    public static string? Validate(
        QuestionType questionType,
        string? options,
        string? correctAnswer,
        IStringLocalizer<SharedResources> localizer)
    {
        return questionType switch
        {
            QuestionType.MCQ         => ValidateMcq(options, correctAnswer, localizer),
            QuestionType.TrueFalse   => ValidateTrueFalse(correctAnswer, localizer),
            QuestionType.Matching    => ValidateMatching(options, localizer),
            QuestionType.FillInBlank => ValidateFillInBlank(correctAnswer, localizer),
            _                        => null
        };
    }

    // ── MCQ ──────────────────────────────────────────────────────────────────────
    // Options: JSON array of strings, ≥2 entries.
    // CorrectAnswer: one of the option strings.

    private static string? ValidateMcq(
        string? options,
        string? correctAnswer,
        IStringLocalizer<SharedResources> localizer)
    {
        List<string>? optionList;
        try
        {
            optionList = string.IsNullOrWhiteSpace(options)
                ? null
                : JsonSerializer.Deserialize<List<string>>(options);
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.McqRequiresAtLeastTwoOptions];
        }

        if (optionList is null || optionList.Count < 2)
            return localizer[SharedResourcesKey.McqRequiresAtLeastTwoOptions];

        if (string.IsNullOrWhiteSpace(correctAnswer) ||
            !optionList.Contains(correctAnswer, StringComparer.Ordinal))
            return localizer[SharedResourcesKey.McqCorrectAnswerMustBeValidOption];

        return null;
    }

    // ── True/False ────────────────────────────────────────────────────────────────
    // CorrectAnswer must be "true" or "false" (case-insensitive).

    private static string? ValidateTrueFalse(
        string? correctAnswer,
        IStringLocalizer<SharedResources> localizer)
    {
        if (string.IsNullOrWhiteSpace(correctAnswer))
            return localizer[SharedResourcesKey.TrueFalseCorrectAnswerInvalid];

        var lower = correctAnswer.Trim().ToLowerInvariant();
        if (lower != "true" && lower != "false")
            return localizer[SharedResourcesKey.TrueFalseCorrectAnswerInvalid];

        return null;
    }

    // ── Matching ──────────────────────────────────────────────────────────────────
    // Options: JSON object {"left": [...], "right": [...]} with equal-length arrays.

    private static string? ValidateMatching(
        string? options,
        IStringLocalizer<SharedResources> localizer)
    {
        if (string.IsNullOrWhiteSpace(options))
            return localizer[SharedResourcesKey.MatchingOptionsMustBePaired];

        try
        {
            using var doc = JsonDocument.Parse(options);
            var root = doc.RootElement;

            if (!root.TryGetProperty("left", out var leftEl) ||
                !root.TryGetProperty("right", out var rightEl))
                return localizer[SharedResourcesKey.MatchingOptionsMustBePaired];

            var leftCount = leftEl.GetArrayLength();
            var rightCount = rightEl.GetArrayLength();

            if (leftCount == 0 || leftCount != rightCount)
                return localizer[SharedResourcesKey.MatchingOptionsMustBePaired];
        }
        catch (JsonException)
        {
            return localizer[SharedResourcesKey.MatchingOptionsMustBePaired];
        }

        return null;
    }

    // ── Fill-in-the-blank ─────────────────────────────────────────────────────────
    // CorrectAnswer must be non-empty.

    private static string? ValidateFillInBlank(
        string? correctAnswer,
        IStringLocalizer<SharedResources> localizer)
    {
        if (string.IsNullOrWhiteSpace(correctAnswer))
            return localizer[SharedResourcesKey.FillInBlankCorrectAnswerRequired];

        return null;
    }
}
