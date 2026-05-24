using FluentAssertions;
using Learnexia.Modules.Learning.Application.Features.Questions.Validation;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.Extensions.Localization;
using Resources;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="QuizQuestionTypeValidation"/>.
/// Each test case exercises a valid or invalid shape per QuestionType.
/// A stub <see cref="IStringLocalizer{SharedResources}"/> is used so the
/// tests run without DI / resource files. Errors are compared by non-null presence,
/// not exact text (the exact string is locale-dependent and tested in integration).
/// </summary>
public sealed class QuizQuestionTypeValidationTests
{
    // ── Stub localizer ────────────────────────────────────────────────────────────
    // Returns the key name as the "localized" string so assertions can inspect
    // which key was returned without requiring resource file loading.

    private static readonly IStringLocalizer<SharedResources> Localizer = new StubLocalizer();

    private sealed class StubLocalizer : IStringLocalizer<SharedResources>
    {
        public LocalizedString this[string name] => new(name, name);
        public LocalizedString this[string name, params object[] arguments] => new(name, name);
        public IEnumerable<LocalizedString> GetAllStrings(bool includeParentCultures)
            => Enumerable.Empty<LocalizedString>();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // MCQ
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MCQ_Valid_TwoOptionsCorrectAnswerIsOption_ReturnsNull()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: """["Option A","Option B"]""",
            correctAnswer: "Option A",
            Localizer);

        result.Should().BeNull();
    }

    [Fact]
    public void MCQ_Valid_FourOptionsCorrectAnswerIsLastOption_ReturnsNull()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: """["A","B","C","D"]""",
            correctAnswer: "D",
            Localizer);

        result.Should().BeNull();
    }

    [Fact]
    public void MCQ_Invalid_OnlyOneOption_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: """["Only one"]""",
            correctAnswer: "Only one",
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.McqRequiresAtLeastTwoOptions);
    }

    [Fact]
    public void MCQ_Invalid_EmptyOptions_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: "[]",
            correctAnswer: "X",
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.McqRequiresAtLeastTwoOptions);
    }

    [Fact]
    public void MCQ_Invalid_CorrectAnswerNotInOptions_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: """["A","B"]""",
            correctAnswer: "C",
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.McqCorrectAnswerMustBeValidOption);
    }

    [Fact]
    public void MCQ_Invalid_NullCorrectAnswer_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: """["A","B"]""",
            correctAnswer: null,
            Localizer);

        result.Should().NotBeNull();
    }

    [Fact]
    public void MCQ_Invalid_MalformedOptions_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.MCQ,
            options: "not-json",
            correctAnswer: "A",
            Localizer);

        result.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TrueFalse
    // ══════════════════════════════════════════════════════════════════════════════

    [Theory]
    [InlineData("true")]
    [InlineData("false")]
    [InlineData("True")]
    [InlineData("False")]
    [InlineData("TRUE")]
    [InlineData("FALSE")]
    public void TrueFalse_Valid_CorrectAnswerIsTrueOrFalse_ReturnsNull(string correctAnswer)
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.TrueFalse,
            options: null,
            correctAnswer: correctAnswer,
            Localizer);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData("yes")]
    [InlineData("no")]
    [InlineData("1")]
    [InlineData("")]
    [InlineData(null)]
    public void TrueFalse_Invalid_CorrectAnswerNotTrueOrFalse_ReturnsError(string? correctAnswer)
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.TrueFalse,
            options: null,
            correctAnswer: correctAnswer,
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.TrueFalseCorrectAnswerInvalid);
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Matching
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Matching_Valid_EqualLeftRight_ReturnsNull()
    {
        var options = """{"left":["A","B","C"],"right":["1","2","3"]}""";

        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.Matching,
            options: options,
            correctAnswer: null,
            Localizer);

        result.Should().BeNull();
    }

    [Fact]
    public void Matching_Invalid_UnequalLeftRight_ReturnsError()
    {
        var options = """{"left":["A","B"],"right":["1","2","3"]}""";

        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.Matching,
            options: options,
            correctAnswer: null,
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.MatchingOptionsMustBePaired);
    }

    [Fact]
    public void Matching_Invalid_MissingLeftKey_ReturnsError()
    {
        var options = """{"right":["1","2"]}""";

        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.Matching,
            options: options,
            correctAnswer: null,
            Localizer);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Matching_Invalid_EmptyOptions_ReturnsError()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.Matching,
            options: null,
            correctAnswer: null,
            Localizer);

        result.Should().NotBeNull();
    }

    [Fact]
    public void Matching_Invalid_EmptyArrays_ReturnsError()
    {
        var options = """{"left":[],"right":[]}""";

        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.Matching,
            options: options,
            correctAnswer: null,
            Localizer);

        result.Should().NotBeNull();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FillInBlank
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FillInBlank_Valid_NonEmptyCorrectAnswer_ReturnsNull()
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.FillInBlank,
            options: null,
            correctAnswer: "photosynthesis",
            Localizer);

        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FillInBlank_Invalid_NullOrWhitespaceCorrectAnswer_ReturnsError(string? correctAnswer)
    {
        var result = QuizQuestionTypeValidation.Validate(
            QuestionType.FillInBlank,
            options: null,
            correctAnswer: correctAnswer,
            Localizer);

        result.Should().NotBeNull();
        result.Should().Be(SharedResourcesKey.FillInBlankCorrectAnswerRequired);
    }
}
