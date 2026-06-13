using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="AdaptivityEngine.DecideDifficulty"/> (P3-08 BE-3).
///
/// Covers all 8+ required cases from the Execution Plan:
///   1. Cold-start → Medium + IsDefault=true
///   2. High performance → Hard
///   3. Struggle performance → Easy
///   4. Monotonicity: improving accuracy never lowers score
///   5. Reproducibility: same inputs → identical output
///   6. Medium band: mid-range score lands in Medium
///   7. Time-normalization degradation: AvgTimeSeconds=0 still produces a valid result
///   8. Per-weight variation: changing a single weight shifts the score predictably
///
/// AC coverage:
///   AC1 — engine uses all four signals (accuracy + time + hints + retries).
///   AC2 — deterministic + reproducible (case 5).
///   AC3 — monotonic response (case 4).
/// </summary>
public sealed class AdaptivityEngineTests
{
    // ── Default options (mirrors appsettings.json defaults) ──────────────────────────────────────
    private static AdaptivityOptions DefaultOptions() => new AdaptivityOptions
    {
        WeightAccuracy      = 0.5,
        WeightTime          = 0.2,
        WeightHint          = 0.2,
        WeightRetry         = 0.1,
        HighBand            = 0.8,
        LowBand             = 0.5,
        ExpectedAttempts    = 2,
        FatigueSignalWeight = 0.0,
    };

    // ── Case 1: Cold-start (IsDefault=true) → Medium + IsDefault ─────────────────────────────────

    [Fact]
    public void DecideDifficulty_IsDefault_ReturnsMediumIsDefault()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.0, AvgTimeSeconds: 0.0, HintRate: 0.0, RetryCount: 0, MasteryPct: 0,
            IsDefault: true);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Difficulty.Should().Be(DifficultyLevel.Medium);
        result.IsDefault.Should().BeTrue();
        result.Score.Should().Be(0.0);
    }

    [Fact]
    public void DecideDifficulty_IsDefaultTrueWithPositiveSignals_StillReturnsColdStart()
    {
        // Even with seemingly positive signals, IsDefault=true always triggers the cold-start path.
        var signals = new AdaptivitySignals(
            AccuracyPct: 1.0, AvgTimeSeconds: 10.0, HintRate: 0.0, RetryCount: 0, MasteryPct: 100,
            IsDefault: true);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Difficulty.Should().Be(DifficultyLevel.Medium);
        result.IsDefault.Should().BeTrue();
    }

    // ── Case 2: High performance → Hard ───────────────────────────────────────────────────────────
    // 100% accuracy, fast time (5 s < 30 s expected), 0% hint, 0 retries → score near 1.0 → Hard

    [Fact]
    public void DecideDifficulty_HighPerformance_ReturnsHard()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 1.0, AvgTimeSeconds: 5.0, HintRate: 0.0, RetryCount: 0, MasteryPct: 95,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Difficulty.Should().Be(DifficultyLevel.Hard);
        result.IsDefault.Should().BeFalse();
        result.Score.Should().BeGreaterThanOrEqualTo(0.8);
    }

    // ── Case 3: Struggle performance → Easy ───────────────────────────────────────────────────────
    // 20% accuracy, slow (120 s), 80% hint, 5 retries → score well below 0.5 → Easy

    [Fact]
    public void DecideDifficulty_StrugglingPerformance_ReturnsEasy()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.2, AvgTimeSeconds: 120.0, HintRate: 0.8, RetryCount: 5, MasteryPct: 15,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Difficulty.Should().Be(DifficultyLevel.Easy);
        result.IsDefault.Should().BeFalse();
        result.Score.Should().BeLessThanOrEqualTo(0.5);
    }

    // ── Case 4: Monotonicity — increasing accuracy never lowers score ─────────────────────────────
    // Keep all other signals equal; step accuracy from 0 → 1 and verify score is non-decreasing.

    [Fact]
    public void DecideDifficulty_IncreasingAccuracy_ScoreIsNonDecreasing()
    {
        var opts = DefaultOptions();
        double prevScore = -1.0;

        foreach (var accPct in new[] { 0.0, 0.2, 0.4, 0.6, 0.8, 1.0 })
        {
            var signals = new AdaptivitySignals(
                AccuracyPct: accPct, AvgTimeSeconds: 20.0, HintRate: 0.3, RetryCount: 1, MasteryPct: 50,
                IsDefault: false);

            var score = AdaptivityEngine.DecideDifficulty(signals, opts).Score;
            score.Should().BeGreaterThanOrEqualTo(prevScore,
                $"score for accuracy={accPct} should be >= score for accuracy={accPct - 0.2}");
            prevScore = score;
        }
    }

    // ── Case 5: Reproducibility — identical inputs produce identical output ────────────────────────

    [Fact]
    public void DecideDifficulty_SameInputsTwice_ReturnsSameOutput()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.65, AvgTimeSeconds: 25.0, HintRate: 0.2, RetryCount: 2, MasteryPct: 60,
            IsDefault: false);
        var opts = DefaultOptions();

        var first  = AdaptivityEngine.DecideDifficulty(signals, opts);
        var second = AdaptivityEngine.DecideDifficulty(signals, opts);

        first.Should().Be(second);
    }

    // ── Case 6: Medium band — mid-range score lands in Medium ─────────────────────────────────────
    // 60% accuracy, moderate time (30 s = expected → timeNorm=1), 30% hint, 1 retry
    // score = 0.5*0.6 + 0.2*1.0 + 0.2*0.7 + 0.1*0.5 = 0.30 + 0.20 + 0.14 + 0.05 = 0.69 → Medium

    [Fact]
    public void DecideDifficulty_MidRangePerformance_ReturnsMedium()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.6, AvgTimeSeconds: 30.0, HintRate: 0.3, RetryCount: 1, MasteryPct: 55,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Difficulty.Should().Be(DifficultyLevel.Medium);
        result.Score.Should().BeGreaterThan(0.5).And.BeLessThan(0.8);
    }

    // ── Case 7: Time-normalization degradation — AvgTimeSeconds=0 still returns a valid result ─────

    [Fact]
    public void DecideDifficulty_ZeroAvgTimeSeconds_StillProducesValidDecision()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.7, AvgTimeSeconds: 0.0, HintRate: 0.1, RetryCount: 1, MasteryPct: 70,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        // Must not throw and must return a valid DifficultyLevel.
        result.Should().NotBeNull();
        result.IsDefault.Should().BeFalse();
        result.Difficulty.Should().BeOneOf(DifficultyLevel.Easy, DifficultyLevel.Medium, DifficultyLevel.Hard);

        // Time component was zeroed — score should be lower than if time had contributed.
        var signalsWithTime = new AdaptivitySignals(
            AccuracyPct: 0.7, AvgTimeSeconds: 5.0, HintRate: 0.1, RetryCount: 1, MasteryPct: 70,
            IsDefault: false);
        var resultWithTime = AdaptivityEngine.DecideDifficulty(signalsWithTime, DefaultOptions());
        result.Score.Should().BeLessThanOrEqualTo(resultWithTime.Score);
    }

    // ── Case 8: Per-weight variation — changing the accuracy weight shifts the score predictably ───

    [Fact]
    public void DecideDifficulty_HigherAccuracyWeight_IncreasesScoreForHighAccuracyStudent()
    {
        // High-accuracy student
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.9, AvgTimeSeconds: 25.0, HintRate: 0.1, RetryCount: 1, MasteryPct: 80,
            IsDefault: false);

        var lowAccWeightOpts = new AdaptivityOptions
        {
            WeightAccuracy = 0.1, WeightTime = 0.3, WeightHint = 0.3, WeightRetry = 0.3,
            HighBand = 0.8, LowBand = 0.5, ExpectedAttempts = 2, FatigueSignalWeight = 0.0,
        };
        var highAccWeightOpts = new AdaptivityOptions
        {
            WeightAccuracy = 0.7, WeightTime = 0.1, WeightHint = 0.1, WeightRetry = 0.1,
            HighBand = 0.8, LowBand = 0.5, ExpectedAttempts = 2, FatigueSignalWeight = 0.0,
        };

        var scoreLow  = AdaptivityEngine.DecideDifficulty(signals, lowAccWeightOpts).Score;
        var scoreHigh = AdaptivityEngine.DecideDifficulty(signals, highAccWeightOpts).Score;

        // A higher accuracy weight should result in a higher (or equal) score when accuracy is high (0.9).
        scoreHigh.Should().BeGreaterThanOrEqualTo(scoreLow);
    }

    // ── Case 9: Negative AvgTimeSeconds treated same as zero ─────────────────────────────────────

    [Fact]
    public void DecideDifficulty_NegativeAvgTimeSeconds_TreatedAsZero()
    {
        var signalsNeg = new AdaptivitySignals(
            AccuracyPct: 0.5, AvgTimeSeconds: -10.0, HintRate: 0.2, RetryCount: 1, MasteryPct: 50,
            IsDefault: false);
        var signalsZero = new AdaptivitySignals(
            AccuracyPct: 0.5, AvgTimeSeconds: 0.0, HintRate: 0.2, RetryCount: 1, MasteryPct: 50,
            IsDefault: false);

        var resultNeg  = AdaptivityEngine.DecideDifficulty(signalsNeg,  DefaultOptions());
        var resultZero = AdaptivityEngine.DecideDifficulty(signalsZero, DefaultOptions());

        // Both negative and zero time → time component = 0 → identical scores and decisions.
        resultNeg.Score.Should().Be(resultZero.Score);
        resultNeg.Difficulty.Should().Be(resultZero.Difficulty);
    }

    // ── Case 10: Band boundary — clear high-band signal → Hard ───────────────────────────────────
    // Use signals that clearly exceed HighBand (0.8) rather than landing exactly on the floating-
    // point boundary (IEEE 754 double arithmetic may land just below 0.8 for nominally equal inputs).
    // acc=1.0, timeNorm=1.0 (5s), hintRate=0.0 → hintComp=1.0, retryCount=0 → retryComp=1.0
    // score = 0.5*1.0 + 0.2*1.0 + 0.2*1.0 + 0.1*1.0 = 1.0 → well above HighBand → Hard

    [Fact]
    public void DecideDifficulty_ClearHighBandSignals_ReturnsHard()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 1.0, AvgTimeSeconds: 5.0, HintRate: 0.0, RetryCount: 0, MasteryPct: 95,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Score.Should().BeGreaterThanOrEqualTo(0.8);
        result.Difficulty.Should().Be(DifficultyLevel.Hard);
    }

    // ── Case 11: Band boundary — clear low-band signal → Easy ────────────────────────────────────
    // acc=0.0, timeNorm=0 (time unavailable), hintRate=1.0 → hintComp=0, retryCount=10 → retryComp=0
    // score = 0 + 0 + 0 + 0 = 0.0 → well below LowBand → Easy

    [Fact]
    public void DecideDifficulty_ClearLowBandSignals_ReturnsEasy()
    {
        var signals = new AdaptivitySignals(
            AccuracyPct: 0.0, AvgTimeSeconds: 0.0, HintRate: 1.0, RetryCount: 10, MasteryPct: 0,
            IsDefault: false);

        var result = AdaptivityEngine.DecideDifficulty(signals, DefaultOptions());

        result.Score.Should().BeLessThanOrEqualTo(0.5);
        result.Difficulty.Should().Be(DifficultyLevel.Easy);
    }
}
