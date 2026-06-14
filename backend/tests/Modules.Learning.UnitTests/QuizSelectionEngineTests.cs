using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="QuizSelectionEngine.Select"/> (P3-11-BE-3).
///
/// All fixtures are built via object initializers — no DbContext, no DI required.
/// QuizQuestion Ids are deterministic integers.
///
/// Required cases (7):
/// 1.  AllHard_TargetHard_ReturnsAllHard
/// 2.  Mixed_TargetHard_SkewedTowardHard
/// 3.  Mixed_TargetEasy_SkewedTowardEasy
/// 4.  ColdStartMedium_TargetMedium_ReturnsMixedOrMedium
/// 5.  ThinPool_OnlyEasy_TargetHard_ReturnsEasyNoError
/// 6.  Determinism_SameInputs_SameOutput
/// 7.  EmptyPool_ReturnsEmptyNoException
///
/// AC coverage:
///   AC2 — direction (Hard/Easy skew), AC3 — cold-start balanced, AC4 — deterministic.
/// </summary>
public sealed class QuizSelectionEngineTests
{
    // ── Default options (mirrors appsettings.json defaults) ──────────────────────────────────────
    private static QuizSelectionOptions DefaultOptions() => new QuizSelectionOptions
    {
        HardRatio   = 0.7,
        MediumRatio = 0.6,
        EasyRatio   = 0.7,
        BlendRatio  = 0.3
    };

    // ── Fixture helpers ───────────────────────────────────────────────────────────────────────────
    private static QuizQuestion MakeQuestion(int id, DifficultyLevel difficulty) =>
        new()
        {
            Id             = id,
            LessonId       = 1,
            QuestionType   = QuestionType.MCQ,
            QuestionText   = $"Q{id}",
            Options        = "[]",
            CorrectAnswer  = "\"A\"",
            Difficulty     = difficulty,
            GeneratedBy    = GeneratedBy.Curated,
            IsActive       = true,
            LifecycleState = LifecycleState.Published
        };

    // ── Case 1: All-Hard pool + target Hard → all Hard returned ──────────────────────────────────

    [Fact]
    public void Select_AllHardPool_TargetHard_ReturnsAllHard()
    {
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1, DifficultyLevel.Hard),
            MakeQuestion(2, DifficultyLevel.Hard),
            MakeQuestion(3, DifficultyLevel.Hard),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, DefaultOptions());

        result.Should().NotBeEmpty();
        result.Should().AllSatisfy(q => q.Difficulty.Should().Be(DifficultyLevel.Hard));
    }

    // ── Case 2: Mixed pool + target Hard → skewed toward Hard (more Hard than Easy) ──────────────

    [Fact]
    public void Select_MixedPool_TargetHard_SkewedTowardHard()
    {
        // 4 Hard, 3 Medium, 3 Easy = 10 questions
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1,  DifficultyLevel.Easy),
            MakeQuestion(2,  DifficultyLevel.Easy),
            MakeQuestion(3,  DifficultyLevel.Easy),
            MakeQuestion(4,  DifficultyLevel.Medium),
            MakeQuestion(5,  DifficultyLevel.Medium),
            MakeQuestion(6,  DifficultyLevel.Medium),
            MakeQuestion(7,  DifficultyLevel.Hard),
            MakeQuestion(8,  DifficultyLevel.Hard),
            MakeQuestion(9,  DifficultyLevel.Hard),
            MakeQuestion(10, DifficultyLevel.Hard),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, DefaultOptions());

        result.Should().NotBeEmpty();
        var hardCount = result.Count(q => q.Difficulty == DifficultyLevel.Hard);
        var easyCount = result.Count(q => q.Difficulty == DifficultyLevel.Easy);

        // With HardRatio=0.7 and a 10-question pool, expect ≥5 Hard (70% of 10 = 7, capped to 4 available).
        // At minimum, more Hard than Easy should be in the result.
        hardCount.Should().BeGreaterThan(easyCount,
            "target Hard with ratio 0.7 must skew toward Hard questions");
    }

    // ── Case 3: Mixed pool + target Easy → skewed toward Easy (more Easy than Hard) ──────────────

    [Fact]
    public void Select_MixedPool_TargetEasy_SkewedTowardEasy()
    {
        // 4 Easy, 3 Medium, 3 Hard = 10 questions
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1,  DifficultyLevel.Easy),
            MakeQuestion(2,  DifficultyLevel.Easy),
            MakeQuestion(3,  DifficultyLevel.Easy),
            MakeQuestion(4,  DifficultyLevel.Easy),
            MakeQuestion(5,  DifficultyLevel.Medium),
            MakeQuestion(6,  DifficultyLevel.Medium),
            MakeQuestion(7,  DifficultyLevel.Medium),
            MakeQuestion(8,  DifficultyLevel.Hard),
            MakeQuestion(9,  DifficultyLevel.Hard),
            MakeQuestion(10, DifficultyLevel.Hard),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Easy, DefaultOptions());

        result.Should().NotBeEmpty();
        var easyCount = result.Count(q => q.Difficulty == DifficultyLevel.Easy);
        var hardCount = result.Count(q => q.Difficulty == DifficultyLevel.Hard);

        easyCount.Should().BeGreaterThan(hardCount,
            "target Easy with ratio 0.7 must skew toward Easy questions");
    }

    // ── Case 4: Cold-start Medium target → balanced/mixed result ─────────────────────────────────
    // When target = Medium the adjacent buckets (Easy + Hard) fill the blend fraction.
    // The result should contain Medium and may include Easy/Hard — never an error.

    [Fact]
    public void Select_MediumTarget_ReturnsMediumAndAdjacentMix()
    {
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1, DifficultyLevel.Easy),
            MakeQuestion(2, DifficultyLevel.Easy),
            MakeQuestion(3, DifficultyLevel.Medium),
            MakeQuestion(4, DifficultyLevel.Medium),
            MakeQuestion(5, DifficultyLevel.Medium),
            MakeQuestion(6, DifficultyLevel.Hard),
            MakeQuestion(7, DifficultyLevel.Hard),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Medium, DefaultOptions());

        result.Should().NotBeEmpty();
        // Medium questions must appear (the target bucket is non-empty).
        result.Should().Contain(q => q.Difficulty == DifficultyLevel.Medium,
            "target Medium must include Medium questions when the bucket is non-empty");
        // Should not throw — result is valid.
        result.Count.Should().BeGreaterThan(0);
    }

    // ── Case 5: Thin pool (only Easy) + target Hard → returns Easy without error ─────────────────
    // Target bucket is empty → graceful degradation: serve all available (Easy).

    [Fact]
    public void Select_ThinPool_OnlyEasy_TargetHard_ReturnsEasyNoError()
    {
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1, DifficultyLevel.Easy),
            MakeQuestion(2, DifficultyLevel.Easy),
            MakeQuestion(3, DifficultyLevel.Easy),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, DefaultOptions());

        // Must not throw. Must not be empty. All returned should be Easy (graceful fallback).
        result.Should().NotBeEmpty("thin pool should never produce an empty result when candidates exist");
        result.Should().AllSatisfy(q => q.Difficulty.Should().Be(DifficultyLevel.Easy));
        result.Count.Should().Be(candidates.Count);
    }

    // ── Case 6: Determinism — same inputs produce the same output ─────────────────────────────────

    [Fact]
    public void Select_SameInputsTwice_ReturnsSameOutput()
    {
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(3,  DifficultyLevel.Easy),
            MakeQuestion(1,  DifficultyLevel.Hard),   // deliberately unsorted to test sort
            MakeQuestion(7,  DifficultyLevel.Medium),
            MakeQuestion(5,  DifficultyLevel.Hard),
            MakeQuestion(2,  DifficultyLevel.Easy),
            MakeQuestion(4,  DifficultyLevel.Medium),
            MakeQuestion(6,  DifficultyLevel.Hard),
        };
        var opts = DefaultOptions();

        var first  = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, opts);
        var second = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, opts);

        first.Select(q => q.Id).Should().BeEquivalentTo(
            second.Select(q => q.Id),
            options => options.WithStrictOrdering(),
            "same inputs must always produce the same ordered result");
    }

    // ── Case 7: Empty pool → empty result, no exception ───────────────────────────────────────────

    [Fact]
    public void Select_EmptyPool_ReturnsEmptyNoException()
    {
        var result = QuizSelectionEngine.Select(
            Array.Empty<QuizQuestion>(),
            DifficultyLevel.Medium,
            DefaultOptions());

        result.Should().NotBeNull();
        result.Should().BeEmpty("an empty input pool must produce an empty result without throwing");
    }

    // ── Bonus case 8: Result is sorted by Id (deterministic ordering) ─────────────────────────────

    [Fact]
    public void Select_Result_IsSortedById()
    {
        // Provide candidates in reverse Id order to verify the engine reorders them.
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(10, DifficultyLevel.Hard),
            MakeQuestion(9,  DifficultyLevel.Hard),
            MakeQuestion(8,  DifficultyLevel.Medium),
            MakeQuestion(7,  DifficultyLevel.Medium),
            MakeQuestion(6,  DifficultyLevel.Easy),
            MakeQuestion(5,  DifficultyLevel.Easy),
        };

        var result = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, DefaultOptions());

        result.Should().NotBeEmpty();
        // Verify ascending Id ordering.
        result.Select(q => q.Id).Should().BeInAscendingOrder(
            "the engine must sort by Id for resume determinism (AC4)");
    }

    // ── Bonus case 9: Mixed pool + target Hard, different ratios → ratio respected ─────────────────

    [Fact]
    public void Select_HighHardRatio_TakesMoreHardQuestions()
    {
        var candidates = new List<QuizQuestion>
        {
            MakeQuestion(1, DifficultyLevel.Easy),
            MakeQuestion(2, DifficultyLevel.Medium),
            MakeQuestion(3, DifficultyLevel.Hard),
            MakeQuestion(4, DifficultyLevel.Hard),
            MakeQuestion(5, DifficultyLevel.Hard),
            MakeQuestion(6, DifficultyLevel.Hard),
        };

        var highHardOpts = new QuizSelectionOptions { HardRatio = 0.9, MediumRatio = 0.6, EasyRatio = 0.7, BlendRatio = 0.1 };
        var lowHardOpts  = new QuizSelectionOptions { HardRatio = 0.3, MediumRatio = 0.6, EasyRatio = 0.7, BlendRatio = 0.7 };

        var highResult = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, highHardOpts);
        var lowResult  = QuizSelectionEngine.Select(candidates, DifficultyLevel.Hard, lowHardOpts);

        var highHardCount = highResult.Count(q => q.Difficulty == DifficultyLevel.Hard);
        var lowHardCount  = lowResult.Count(q  => q.Difficulty == DifficultyLevel.Hard);

        highHardCount.Should().BeGreaterThanOrEqualTo(lowHardCount,
            "higher HardRatio should yield at least as many Hard questions");
    }
}
