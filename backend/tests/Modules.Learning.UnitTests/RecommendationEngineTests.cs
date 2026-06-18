using FluentAssertions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Resources;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="RecommendationEngine.Compute"/> (P5-09-BE-2).
///
/// Covers the required cases from the Execution Plan:
///   1.  Determinism — same inputs produce identical output.
///   2.  Cap — never more than 5 items.
///   3.  Ranking — severity descending, then mastery deficit descending.
///   4.  Cold-start (empty weak areas) — well-formed Celebrate item, never empty.
///   5.  Review action-type for High-severity area.
///   6.  Practice action-type for Medium-severity area.
///   7.  AdaptivityDecision propagated to TargetDifficulty.
///   8.  Missing adaptivity decision defaults to Medium difficulty.
///   9.  Grade is not required (null grade runs without error).
///   10. All items carry non-empty i18n keys (no inline text).
/// </summary>
public sealed class RecommendationEngineTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static DerivedProfile ColdProfile() => new(
        QuestionTypeAffinity:     new Dictionary<string, double>(),
        RecurringErrorSkillIds:   Array.Empty<int>(),
        AttentionSpanMinutes:     null,
        PreferredExplanationStyle: ExplanationStyle.Standard,
        DataPointCount:           0);

    private static DerivedProfile RichProfile() => new(
        QuestionTypeAffinity:     new Dictionary<string, double> { ["MCQ"] = 0.8 },
        RecurringErrorSkillIds:   new[] { 1, 2 },
        AttentionSpanMinutes:     20,
        PreferredExplanationStyle: ExplanationStyle.Standard,
        DataPointCount:           50);

    private static WeakAreaEntry HighArea(int skillId = 1, int subjectCode = 0, int masteryPct = 20)
        => new(skillId, $"Skill{skillId}", subjectCode, masteryPct, WeakAreaSeverity.High, "ReviewConcept");

    private static WeakAreaEntry MediumArea(int skillId = 2, int subjectCode = 1, int masteryPct = 40)
        => new(skillId, $"Skill{skillId}", subjectCode, masteryPct, WeakAreaSeverity.Medium, "PracticeSkill");

    private static WeakAreaEntry LowArea(int skillId = 3, int subjectCode = 2, int masteryPct = 55)
        => new(skillId, $"Skill{skillId}", subjectCode, masteryPct, WeakAreaSeverity.Low, "PracticeSkill");

    private static IReadOnlyDictionary<int, AdaptivityDecision> NoDecisions()
        => new Dictionary<int, AdaptivityDecision>();

    private static IReadOnlyDictionary<int, AdaptivityDecision> DecisionForSkill(
        int skillId, DifficultyLevel difficulty)
        => new Dictionary<int, AdaptivityDecision>
        {
            [skillId] = new AdaptivityDecision(difficulty, IsDefault: false, Score: 0.7),
        };

    // ── Tests ─────────────────────────────────────────────────────────────────────────────────────

    [Fact]
    public void Compute_SameInputs_ProducesIdenticalOutput()
    {
        // Determinism — same inputs → same output.
        var areas    = new[] { HighArea(1), MediumArea(2) };
        var decisions = NoDecisions();
        var profile  = RichProfile();

        var first  = RecommendationEngine.Compute(areas, decisions, profile, grade: 3);
        var second = RecommendationEngine.Compute(areas, decisions, profile, grade: 3);

        first.Should().BeEquivalentTo(second, "deterministic: same inputs must produce same output");
    }

    [Fact]
    public void Compute_WhenMoreThanFiveWeakAreas_CapsAtFive()
    {
        // Cap at MaxItems = 5.
        var areas = Enumerable.Range(1, 10)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result.Length.Should().Be(5, "engine caps output at 5 items");
    }

    [Fact]
    public void Compute_RanksBySeverityDescendingThenMasteryDeficit()
    {
        // High-severity items come before Medium regardless of order in input.
        var areas = new[]
        {
            MediumArea(skillId: 10, masteryPct: 45),  // lower deficit
            HighArea(skillId: 20, masteryPct: 10),     // high severity
            MediumArea(skillId: 30, masteryPct: 35),  // higher deficit than skillId=10
        };

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].SkillId.Should().Be(20, "High-severity item must come first");
        // Among the two Medium items, higher mastery deficit (100-35=65) > (100-45=55) → skillId=30 next.
        result[1].SkillId.Should().Be(30, "Among Medium items, higher mastery deficit comes first");
        result[2].SkillId.Should().Be(10, "Lower mastery deficit Medium item comes last");
    }

    [Fact]
    public void Compute_WhenNoWeakAreas_ReturnsColdStartCelebrateItem()
    {
        // Cold-start: empty weak areas → encouraging Celebrate item, never empty.
        var result = RecommendationEngine.Compute(
            weakAreas:           Array.Empty<WeakAreaEntry>(),
            adaptivityDecisions: NoDecisions(),
            profile:             ColdProfile(),
            grade:               null);

        result.Should().HaveCount(1, "cold-start returns exactly one encouraging item");
        result[0].ActionType.Should().Be(RecommendationActionType.Celebrate);
        result[0].TitleKey.Should().NotBeNullOrWhiteSpace();
        result[0].BodyKey.Should().NotBeNullOrWhiteSpace();
        result[0].CtaKey.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Compute_HighSeverityArea_ProducesReviewActionType()
    {
        var areas  = new[] { HighArea() };
        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review);
        result[0].TitleKey.Should().Be(SharedResourcesKey.RecReviewTitle);
    }

    [Fact]
    public void Compute_MediumSeverityArea_ProducesPracticeActionType()
    {
        var areas  = new[] { MediumArea() };
        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice);
        result[0].TitleKey.Should().Be(SharedResourcesKey.RecPracticeTitle);
    }

    [Fact]
    public void Compute_LowSeverityWithColdProfile_ProducesCelebrateActionType()
    {
        // DataPointCount == 0 + Low severity → Celebrate (encouraging cold-start framing).
        var areas  = new[] { LowArea() };
        var result = RecommendationEngine.Compute(areas, NoDecisions(), ColdProfile(), grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Celebrate);
    }

    [Fact]
    public void Compute_LowSeverityWithRichProfile_ProducesPracticeActionType()
    {
        // DataPointCount > 0 + Low severity → Practice (drifting; practise to re-solidify).
        var areas  = new[] { LowArea() };
        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice);
    }

    [Fact]
    public void Compute_PropagatesAdaptivityDecisionToTargetDifficulty()
    {
        // TargetDifficulty from adaptivity decision is propagated to the item.
        var areas     = new[] { HighArea(skillId: 5) };
        var decisions = DecisionForSkill(skillId: 5, DifficultyLevel.Easy);

        var result = RecommendationEngine.Compute(areas, decisions, RichProfile(), grade: null);

        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Easy);
    }

    [Fact]
    public void Compute_WhenAdaptivityDecisionMissing_DefaultsToMediumDifficulty()
    {
        // No decision in map → engine defaults to Medium.
        var areas  = new[] { HighArea(skillId: 99) };

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Medium);
    }

    [Fact]
    public void Compute_NullGrade_DoesNotThrowAndProducesValidOutput()
    {
        // Grade is optional — null must not throw or produce empty/invalid output.
        var areas  = new[] { MediumArea() };

        var act    = () => RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        act.Should().NotThrow();
        var result = act();
        result.Should().HaveCount(1);
    }

    [Fact]
    public void Compute_AllItems_HaveNonEmptyI18nKeys()
    {
        // No inline text — all key fields must be non-empty strings.
        var areas = new[] { HighArea(1), MediumArea(2), LowArea(3) };

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: 5);

        foreach (var item in result)
        {
            item.TitleKey.Should().NotBeNullOrWhiteSpace($"SkillId={item.SkillId} TitleKey must be set");
            item.BodyKey.Should().NotBeNullOrWhiteSpace($"SkillId={item.SkillId} BodyKey must be set");
            item.CtaKey.Should().NotBeNullOrWhiteSpace($"SkillId={item.SkillId} CtaKey must be set");
        }
    }

    [Fact]
    public void Compute_SubjectCodeIsPreservedFromWeakAreaEntry()
    {
        // SubjectCode from the WeakAreaEntry must flow through unchanged.
        var areas = new[] { MediumArea(skillId: 7, subjectCode: 3 /* ENGLISH */) };

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result[0].SubjectCode.Should().Be(3);
    }
}
