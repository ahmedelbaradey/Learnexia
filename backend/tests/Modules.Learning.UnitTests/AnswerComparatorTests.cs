using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="AnswerComparator.AreEqual"/>.
/// Each test case exercises a specific <see cref="QuestionType"/> comparison rule
/// or a null/empty guard invariant. No DI or DbContext required — the helper is pure static.
/// </summary>
public sealed class AnswerComparatorTests
{
    // ══════════════════════════════════════════════════════════════════════════════
    // MCQ
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MCQ_ExactMatchCaseInsensitive_ReturnsTrue()
    {
        // "A" vs "a" — MCQ uses OrdinalIgnoreCase; case difference must not matter.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "A", correctAnswer: "a");

        result.Should().BeTrue();
    }

    [Fact]
    public void MCQ_WrongOption_ReturnsFalse()
    {
        // "A" vs "B" — different option selected.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "A", correctAnswer: "B");

        result.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // TrueFalse
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void TrueFalse_BothParseTrueMatchingValues_ReturnsTrue()
    {
        // "true" vs "True" — both parse to bool true; values match.
        var result = AnswerComparator.AreEqual(QuestionType.TrueFalse, studentPayload: "true", correctAnswer: "True");

        result.Should().BeTrue();
    }

    [Fact]
    public void TrueFalse_BothParseBoolButValuesDiffer_ReturnsFalse()
    {
        // "True" vs "false" — both parse successfully but booleans differ.
        var result = AnswerComparator.AreEqual(QuestionType.TrueFalse, studentPayload: "True", correctAnswer: "false");

        result.Should().BeFalse();
    }

    [Fact]
    public void TrueFalse_NonBoolPayload_ReturnsFalse()
    {
        // "yes" does not satisfy bool.TryParse — must return false, not throw.
        var result = AnswerComparator.AreEqual(QuestionType.TrueFalse, studentPayload: "yes", correctAnswer: "true");

        result.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // FillInBlank
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void FillInBlank_TrimmedAndCaseInsensitiveMatch_ReturnsTrue()
    {
        // "  cairo  " vs "Cairo" — leading/trailing whitespace trimmed before compare.
        var result = AnswerComparator.AreEqual(QuestionType.FillInBlank, studentPayload: "  cairo  ", correctAnswer: "Cairo");

        result.Should().BeTrue();
    }

    [Fact]
    public void FillInBlank_OnlyCaseDifference_ReturnsTrue()
    {
        // "PARIS" vs "paris" — pure case difference, must still match.
        var result = AnswerComparator.AreEqual(QuestionType.FillInBlank, studentPayload: "PARIS", correctAnswer: "paris");

        result.Should().BeTrue();
    }

    [Fact]
    public void FillInBlank_TabAndNewlineWhitespaceTrimmed_ReturnsTrue()
    {
        // "\tparis\n" — tab and newline are whitespace; Trim() removes them.
        var result = AnswerComparator.AreEqual(QuestionType.FillInBlank, studentPayload: "\tparis\n", correctAnswer: "paris");

        result.Should().BeTrue();
    }

    [Fact]
    public void FillInBlank_DifferentWords_ReturnsFalse()
    {
        // "Cairo" vs "Giza" — distinct cities, must not match.
        var result = AnswerComparator.AreEqual(QuestionType.FillInBlank, studentPayload: "Cairo", correctAnswer: "Giza");

        result.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Matching
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void Matching_IdenticalStrings_ReturnsTrue()
    {
        // Matching currently falls through to string compare (TODO P2-07.b).
        // When both sides are identical the fallback must return true.
        var result = AnswerComparator.AreEqual(QuestionType.Matching, studentPayload: "1-A;2-B", correctAnswer: "1-A;2-B");

        result.Should().BeTrue();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // Null / empty guard — must not throw, must return false
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void NullPayload_DoesNotThrow_ReturnsFalse()
    {
        // null studentPayload — guard returns false without throwing.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: null, correctAnswer: "A");

        result.Should().BeFalse();
    }

    [Fact]
    public void EmptyPayload_DoesNotThrow_ReturnsFalse()
    {
        // empty string studentPayload — guard returns false without throwing.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "", correctAnswer: "A");

        result.Should().BeFalse();
    }

    [Fact]
    public void NullCorrectAnswer_DoesNotThrow_ReturnsFalse()
    {
        // null correctAnswer — guard returns false without throwing.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "A", correctAnswer: null);

        result.Should().BeFalse();
    }

    // ══════════════════════════════════════════════════════════════════════════════
    // DEF-P205FE-01 regression — CorrectAnswer is persisted as jsonb (JSON-encoded).
    // The seeder/admin store the string "6" as the 3-char text "6" (with quotes); the
    // student submits the raw value 6. The comparator must decode the JSON-string wrapping
    // before comparing, otherwise EVERY MCQ/TrueFalse/FillInBlank graded incorrectly.
    // ══════════════════════════════════════════════════════════════════════════════

    [Fact]
    public void MCQ_JsonEncodedCorrectAnswer_MatchesRawStudentPayload()
    {
        // correctAnswer stored as jsonb "6"; student submits 6 → must be correct.
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "6", correctAnswer: "\"6\"");

        result.Should().BeTrue();
    }

    [Fact]
    public void MCQ_JsonEncodedCorrectAnswer_WrongOption_ReturnsFalse()
    {
        // correctAnswer jsonb "6"; student submits 5 → still incorrect (no false positives).
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "5", correctAnswer: "\"6\"");

        result.Should().BeFalse();
    }

    [Fact]
    public void MCQ_JsonEncodedArabicCorrectAnswer_Matches()
    {
        // Arabic value round-trips through JSON decode (seeder stores "شجرة").
        var result = AnswerComparator.AreEqual(QuestionType.MCQ, studentPayload: "شجرة", correctAnswer: "\"شجرة\"");

        result.Should().BeTrue();
    }

    [Fact]
    public void TrueFalse_JsonEncodedCorrectAnswer_Matches()
    {
        // correctAnswer jsonb "true"; bool.TryParse only works after the quotes are decoded.
        var result = AnswerComparator.AreEqual(QuestionType.TrueFalse, studentPayload: "true", correctAnswer: "\"true\"");

        result.Should().BeTrue();
    }

    [Fact]
    public void FillInBlank_JsonEncodedCorrectAnswer_Matches()
    {
        // correctAnswer jsonb "Tree"; student submits Tree → correct after decode.
        var result = AnswerComparator.AreEqual(QuestionType.FillInBlank, studentPayload: "Tree", correctAnswer: "\"Tree\"");

        result.Should().BeTrue();
    }
}
