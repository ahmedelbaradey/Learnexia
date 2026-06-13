using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="MasteryEngine.Compute"/>.
///
/// Verifies the 4-state machine (P3-09 Q2 — per-skill threshold + 50% floor),
/// the cumulative accuracy formula, boundary conditions, reproducibility, and per-skill threshold variation.
/// </summary>
public sealed class MasteryEngineTests
{
    // ── Case 1: zero answers → NotStarted ────────────────────────────────────

    [Fact]
    public void Compute_ZeroAnswers_ReturnsNotStarted()
    {
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 0, correctAnswers: 0, masteryThreshold: 80);

        pct.Should().Be(0);
        status.Should().Be(MasteryStatus.NotStarted);
    }

    // ── Case 2: pct exactly at threshold → Mastered ──────────────────────────

    [Fact]
    public void Compute_ExactThreshold_ReturnsMastered()
    {
        // 8 correct out of 10 = 80% with threshold 80 → Mastered
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 10, correctAnswers: 8, masteryThreshold: 80);

        pct.Should().Be(80);
        status.Should().Be(MasteryStatus.Mastered);
    }

    // ── Case 3: pct below threshold but ≥ 50 → InProgress ──────────────────

    [Fact]
    public void Compute_BelowThresholdAboveFloor_ReturnsInProgress()
    {
        // 6 correct out of 10 = 60% with threshold 80 → InProgress
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 10, correctAnswers: 6, masteryThreshold: 80);

        pct.Should().Be(60);
        status.Should().Be(MasteryStatus.InProgress);
    }

    // ── Case 4: pct below 50 → NeedsReview ──────────────────────────────────

    [Fact]
    public void Compute_Below50Percent_ReturnsNeedsReview()
    {
        // 4 correct out of 10 = 40% → NeedsReview
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 10, correctAnswers: 4, masteryThreshold: 80);

        pct.Should().Be(40);
        status.Should().Be(MasteryStatus.NeedsReview);
    }

    // ── Case 5: reproducibility — same inputs → same outputs ─────────────────

    [Fact]
    public void Compute_SameInputsTwice_ReturnsSameOutput()
    {
        var first  = MasteryEngine.Compute(totalAnswers: 20, correctAnswers: 14, masteryThreshold: 75);
        var second = MasteryEngine.Compute(totalAnswers: 20, correctAnswers: 14, masteryThreshold: 75);

        first.Should().Be(second);
    }

    // ── Case 6: per-skill threshold variation — 70 vs 80 vs 85 ──────────────

    [Fact]
    public void Compute_PerSkillThresholdVariation_RespectsEachThreshold()
    {
        // 72 correct out of 100 = 72%
        var (pct70, status70) = MasteryEngine.Compute(totalAnswers: 100, correctAnswers: 72, masteryThreshold: 70);
        var (pct80, status80) = MasteryEngine.Compute(totalAnswers: 100, correctAnswers: 72, masteryThreshold: 80);
        var (pct85, status85) = MasteryEngine.Compute(totalAnswers: 100, correctAnswers: 72, masteryThreshold: 85);

        pct70.Should().Be(72);
        status70.Should().Be(MasteryStatus.Mastered);    // 72 ≥ 70

        pct80.Should().Be(72);
        status80.Should().Be(MasteryStatus.InProgress);  // 50 ≤ 72 < 80

        pct85.Should().Be(72);
        status85.Should().Be(MasteryStatus.InProgress);  // 50 ≤ 72 < 85
    }

    // ── Case 7: boundary exactly at 50% floor ────────────────────────────────

    [Fact]
    public void Compute_Exactly50Percent_ReturnsInProgress()
    {
        // 5 correct out of 10 = exactly 50% → InProgress (not NeedsReview)
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 10, correctAnswers: 5, masteryThreshold: 80);

        pct.Should().Be(50);
        status.Should().Be(MasteryStatus.InProgress);
    }

    // ── Case 8: boundary just below threshold → InProgress ───────────────────

    [Fact]
    public void Compute_OneAnswerBelowThreshold_ReturnsInProgress()
    {
        // 79 correct out of 100 = 79% with threshold 80 → InProgress (not Mastered)
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 100, correctAnswers: 79, masteryThreshold: 80);

        pct.Should().Be(79);
        status.Should().Be(MasteryStatus.InProgress);
    }

    // ── Case 9: pct above threshold (over-achievement) → Mastered ────────────

    [Fact]
    public void Compute_AboveThreshold_ReturnsMastered()
    {
        // 95 correct out of 100 = 95% with threshold 80 → Mastered
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 100, correctAnswers: 95, masteryThreshold: 80);

        pct.Should().Be(95);
        status.Should().Be(MasteryStatus.Mastered);
    }

    // ── Case 10: low-count rounding — 1/3 rounds to 33 → NeedsReview ────────

    [Fact]
    public void Compute_LowCountRounding_RoundsCorrectly()
    {
        // 1 correct out of 3 = 33.33% → rounds to 33 → NeedsReview
        var (pct, status) = MasteryEngine.Compute(totalAnswers: 3, correctAnswers: 1, masteryThreshold: 80);

        pct.Should().Be(33);
        status.Should().Be(MasteryStatus.NeedsReview);
    }
}
