using FluentAssertions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="StudentProfileEngine.Derive"/> (P3-13 BE-4).
///
/// Covers the 10+ required cases from the Execution Plan:
///   1.  Cold-start → neutral defaults (DataPointCount=0, Standard, empty affinity/clusters, null attention)
///   2.  Strong MCQ accuracy → MCQ appears in affinity
///   3.  Weak FillInBlank accuracy → FillInBlank NOT in affinity (below overall rate)
///   4.  Recurring errors ≥ threshold on same skill → skill appears in clusters
///   5.  Recurring errors below threshold → skill NOT in clusters
///   6.  Accuracy drop in later minute-bucket → attention span detected (non-null)
///   7.  Attention span with insufficient bucket data (fewer than 2 buckets) → null
///   8.  Explanation style Standard on cold-start (even with hints present)
///   9.  Explanation style Visual when Matching affinity + high hint rate
///   10. Explanation style StepByStep when FillInBlank affinity + low hint rate
///   11. Reproducibility: same signals twice → identical output
///   12. Min-sample guard: type with fewer answers than MinSamplePerQuestionType excluded from affinity
///
/// AC coverage:
///   AC1 — all four attributes derived by the engine.
///   AC2 — rule-based, deterministic, reproducible (case 11).
///   AC4 — cold-start path returns safe defaults (case 1).
/// </summary>
public sealed class StudentProfileEngineTests
{
    // ── Default options (mirrors appsettings.json StudentProfile:Engine defaults) ─────────────────

    private static StudentProfileOptions DefaultOptions() => new StudentProfileOptions
    {
        MinSamplePerQuestionType    = 5,
        RecurringErrorThreshold     = 3,
        AccuracyDropThreshold       = 0.15,
        AttentionWindowMinutes      = 20,
        ColdStartDataPointThreshold = 10,
    };

    // ── Helpers to build minimal valid signals ────────────────────────────────────────────────────

    private static StudentSignals BuildSignals(
        int totalAnswers = 20,
        double overallAccuracy = 0.7,
        IReadOnlyList<(QuestionType, int correct, int total)>? answersByType = null,
        IReadOnlyList<(int skillId, int wrongCount)>? skillErrorCounts = null,
        IReadOnlyList<(int minuteBucket, double accuracy)>? sessionBuckets = null,
        IReadOnlyList<(QuestionType type, int hintCount, int total)>? hintByType = null)
    {
        return new StudentSignals(
            AnswersByType:          answersByType      ?? Array.Empty<(QuestionType, int, int)>(),
            SkillErrorCounts:       skillErrorCounts   ?? Array.Empty<(int, int)>(),
            SessionAccuracyBuckets: sessionBuckets     ?? Array.Empty<(int, double)>(),
            OverallAccuracy:        overallAccuracy,
            TotalAnswers:           totalAnswers,
            HintAnswerCountByType:  hintByType         ?? Array.Empty<(QuestionType, int, int)>());
    }

    // ── Case 1: Cold-start → neutral defaults ────────────────────────────────────────────────────

    [Fact]
    public void Derive_ColdStart_ReturnsNeutralDefaults()
    {
        // TotalAnswers = 5 < ColdStartDataPointThreshold (10) → cold-start path.
        var signals = BuildSignals(totalAnswers: 5);
        var opts    = DefaultOptions();

        var profile = StudentProfileEngine.Derive(signals, opts);

        profile.DataPointCount.Should().Be(0);
        profile.PreferredExplanationStyle.Should().Be(ExplanationStyle.Standard);
        profile.QuestionTypeAffinity.Should().BeEmpty();
        profile.RecurringErrorSkillIds.Should().BeEmpty();
        profile.AttentionSpanMinutes.Should().BeNull();
    }

    [Fact]
    public void Derive_ColdStartExactlyAtThreshold_IsNotColdStart()
    {
        // TotalAnswers == ColdStartDataPointThreshold (10) → NOT cold-start (< is the guard).
        var signals = BuildSignals(totalAnswers: 10, overallAccuracy: 0.6,
            answersByType: new[] { (QuestionType.MCQ, 6, 10) });
        var opts = DefaultOptions();

        var profile = StudentProfileEngine.Derive(signals, opts);

        // DataPointCount must reflect the actual total, not zero.
        profile.DataPointCount.Should().Be(10);
    }

    // ── Case 2: Strong MCQ accuracy → MCQ in affinity ────────────────────────────────────────────

    [Fact]
    public void Derive_StrongMCQAccuracy_MCQAppearsInAffinity()
    {
        // MCQ: 9 correct out of 10 = 0.9; overall = 0.65 → MCQ success rate > overall → affinity.
        var signals = BuildSignals(
            totalAnswers: 20,
            overallAccuracy: 0.65,
            answersByType: new[]
            {
                (QuestionType.MCQ,        9,  10),   // rate 0.9 > 0.65 → affinity
                (QuestionType.TrueFalse,  4,  10),   // rate 0.4 < 0.65 → no affinity
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.QuestionTypeAffinity.Should().ContainKey(QuestionType.MCQ.ToString());
        profile.QuestionTypeAffinity[QuestionType.MCQ.ToString()].Should().BeApproximately(0.9, 0.001);
        profile.QuestionTypeAffinity.Should().NotContainKey(QuestionType.TrueFalse.ToString());
    }

    // ── Case 3: Weak FillInBlank accuracy → NOT in affinity ─────────────────────────────────────

    [Fact]
    public void Derive_WeakFillInBlankAccuracy_NotInAffinity()
    {
        // FillInBlank: 2 correct out of 10 = 0.2; overall = 0.65 → rate < overall → not in affinity.
        var signals = BuildSignals(
            totalAnswers: 20,
            overallAccuracy: 0.65,
            answersByType: new[]
            {
                (QuestionType.FillInBlank, 2, 10),   // 0.2 < 0.65 → excluded
                (QuestionType.MCQ,         9, 10),   // 0.9 > 0.65 → included
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.QuestionTypeAffinity.Should().NotContainKey(QuestionType.FillInBlank.ToString());
    }

    // ── Case 4: Recurring errors ≥ threshold → skill in clusters ─────────────────────────────────

    [Fact]
    public void Derive_RecurringErrorsAtOrAboveThreshold_SkillInClusters()
    {
        // Skill 42: 3 wrong answers = RecurringErrorThreshold (3) → qualifies.
        // Skill 99: 5 wrong answers > threshold → qualifies.
        var signals = BuildSignals(
            totalAnswers: 20,
            skillErrorCounts: new[]
            {
                (42, 3),   // exactly at threshold
                (99, 5),   // above threshold
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.RecurringErrorSkillIds.Should().Contain(42);
        profile.RecurringErrorSkillIds.Should().Contain(99);
    }

    // ── Case 5: Recurring errors below threshold → NOT in clusters ───────────────────────────────

    [Fact]
    public void Derive_RecurringErrorsBelowThreshold_SkillNotInClusters()
    {
        // Skill 7: 2 wrong answers < RecurringErrorThreshold (3) → does not qualify.
        var signals = BuildSignals(
            totalAnswers: 20,
            skillErrorCounts: new[]
            {
                (7, 2),    // below threshold
                (8, 1),    // well below
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.RecurringErrorSkillIds.Should().NotContain(7);
        profile.RecurringErrorSkillIds.Should().NotContain(8);
    }

    // ── Case 6: Accuracy drop in later minute-bucket → attention span detected ───────────────────

    [Fact]
    public void Derive_AccuracyDropInLaterBucket_AttentionSpanDetected()
    {
        // Bucket 0 (0–20 min): 0.85 accuracy; bucket 1 (20–40 min): 0.65 accuracy.
        // Drop = 0.85 - 0.65 = 0.20 >= AccuracyDropThreshold (0.15) → attention span = 20.
        var signals = BuildSignals(
            totalAnswers: 20,
            sessionBuckets: new[]
            {
                (0,  0.85),
                (20, 0.65),
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.AttentionSpanMinutes.Should().Be(20);
    }

    [Fact]
    public void Derive_SmallAccuracyDrop_AttentionSpanNotDetected()
    {
        // Drop = 0.85 - 0.75 = 0.10 < AccuracyDropThreshold (0.15) → no detection.
        var signals = BuildSignals(
            totalAnswers: 20,
            sessionBuckets: new[]
            {
                (0,  0.85),
                (20, 0.75),
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.AttentionSpanMinutes.Should().BeNull();
    }

    // ── Case 7: Attention span with insufficient bucket data → null ──────────────────────────────

    [Fact]
    public void Derive_InsufficientBuckets_AttentionSpanNull()
    {
        // Only 1 bucket (need at least 2 for comparison) → null.
        var signals = BuildSignals(
            totalAnswers: 20,
            sessionBuckets: new[] { (0, 0.9) });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.AttentionSpanMinutes.Should().BeNull();
    }

    [Fact]
    public void Derive_NoBuckets_AttentionSpanNull()
    {
        var signals = BuildSignals(totalAnswers: 20, sessionBuckets: Array.Empty<(int, double)>());

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.AttentionSpanMinutes.Should().BeNull();
    }

    // ── Case 8: Explanation style Standard on cold-start ─────────────────────────────────────────

    [Fact]
    public void Derive_ColdStart_ExplanationStyleIsStandard()
    {
        // Cold-start path always returns Standard regardless of any other signals.
        var signals = BuildSignals(
            totalAnswers: 2,    // < ColdStartDataPointThreshold
            hintByType: new[] { (QuestionType.MCQ, 2, 2) }   // 100% hint rate — should not matter
        );

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.PreferredExplanationStyle.Should().Be(ExplanationStyle.Standard);
    }

    // ── Case 9: Explanation style Visual when Matching affinity + high hint rate ─────────────────

    [Fact]
    public void Derive_MatchingAffinityHighHintRate_ExplanationStyleVisual()
    {
        // Matching: 9/10 = 0.9 > 0.6 overall → affinity. Overall hint rate: 12/20 = 0.6 >= 0.5.
        var signals = BuildSignals(
            totalAnswers: 20,
            overallAccuracy: 0.6,
            answersByType: new[]
            {
                (QuestionType.Matching, 9, 10),   // rate 0.9 > 0.6 → affinity
                (QuestionType.MCQ,      3, 10),   // rate 0.3 < 0.6 → no affinity
            },
            hintByType: new[]
            {
                (QuestionType.MCQ,     8, 10),    // 80% hint rate on MCQ
                (QuestionType.Matching, 4, 10),   // 40% hint rate on Matching
            });
        // Total hints = 8 + 4 = 12 / 20 total answers = 0.6 ≥ 0.5 → Visual heuristic fires.

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.PreferredExplanationStyle.Should().Be(ExplanationStyle.Visual);
    }

    // ── Case 10: Explanation style StepByStep when FillInBlank affinity + low hint rate ──────────

    [Fact]
    public void Derive_FillInBlankAffinityLowHintRate_ExplanationStyleStepByStep()
    {
        // FillInBlank: 9/10 = 0.9 > 0.6 overall → affinity. FillInBlank hint rate: 1/10 = 0.1 < 0.3.
        // Overall hint rate: 1/20 = 0.05 < 0.5 → Simplified heuristic does not fire.
        var signals = BuildSignals(
            totalAnswers: 20,
            overallAccuracy: 0.6,
            answersByType: new[]
            {
                (QuestionType.FillInBlank, 9, 10),   // rate 0.9 > 0.6 → affinity
                (QuestionType.MCQ,         3, 10),   // rate 0.3 < 0.6 → no affinity
            },
            hintByType: new[]
            {
                (QuestionType.FillInBlank, 1, 10),   // 10% hint rate → low
                (QuestionType.MCQ,         0, 10),   // 0% hint rate
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.PreferredExplanationStyle.Should().Be(ExplanationStyle.StepByStep);
    }

    // ── Case 11: Reproducibility — identical inputs produce identical output ──────────────────────

    [Fact]
    public void Derive_SameSignalsTwice_ReturnsSameOutput()
    {
        var signals = BuildSignals(
            totalAnswers: 25,
            overallAccuracy: 0.7,
            answersByType: new[]
            {
                (QuestionType.MCQ,      8, 10),
                (QuestionType.TrueFalse, 6, 10),
                (QuestionType.Matching, 2,  5),
            },
            skillErrorCounts: new[]
            {
                (10, 4),
                (20, 1),
            },
            sessionBuckets: new[]
            {
                (0,  0.88),
                (20, 0.72),
                (40, 0.60),
            });
        var opts = DefaultOptions();

        var first  = StudentProfileEngine.Derive(signals, opts);
        var second = StudentProfileEngine.Derive(signals, opts);

        first.DataPointCount.Should().Be(second.DataPointCount);
        first.PreferredExplanationStyle.Should().Be(second.PreferredExplanationStyle);
        first.AttentionSpanMinutes.Should().Be(second.AttentionSpanMinutes);
        first.RecurringErrorSkillIds.Should().BeEquivalentTo(second.RecurringErrorSkillIds);
        first.QuestionTypeAffinity.Should().BeEquivalentTo(second.QuestionTypeAffinity);
    }

    // ── Case 12: Min-sample guard — below threshold excluded from affinity ────────────────────────

    [Fact]
    public void Derive_TypeBelowMinSample_ExcludedFromAffinity()
    {
        // MCQ: 4 answers = < MinSamplePerQuestionType (5) → excluded even with perfect accuracy.
        // TrueFalse: 5 answers = exactly MinSamplePerQuestionType, high accuracy → included.
        var signals = BuildSignals(
            totalAnswers: 20,
            overallAccuracy: 0.5,
            answersByType: new[]
            {
                (QuestionType.MCQ,       4,  4),   // 4 < 5 min-sample → excluded
                (QuestionType.TrueFalse, 5,  5),   // 5 ≥ 5, rate 1.0 > 0.5 → included
            });

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.QuestionTypeAffinity.Should().NotContainKey(QuestionType.MCQ.ToString());
        profile.QuestionTypeAffinity.Should().ContainKey(QuestionType.TrueFalse.ToString());
    }

    // ── Case 13: DataPointCount reflects TotalAnswers (not cold-start path) ──────────────────────

    [Fact]
    public void Derive_NonColdStart_DataPointCountEqualsTotalAnswers()
    {
        var signals = BuildSignals(totalAnswers: 30);

        var profile = StudentProfileEngine.Derive(signals, DefaultOptions());

        profile.DataPointCount.Should().Be(30);
    }
}
