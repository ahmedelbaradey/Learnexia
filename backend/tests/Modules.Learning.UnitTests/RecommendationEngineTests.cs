using FluentAssertions;
using Learnexia.Modules.Learning.Application.Services;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Resources;
using Xunit;

namespace Modules.Learning.UnitTests;

/// <summary>
/// Unit tests for <see cref="RecommendationEngine.Compute"/> (P5-09-BE-2 + P5-09a-BE-4).
///
/// Original P5-09 coverage (cases 1–11 below) — UNCHANGED, all must still pass.
/// P5-09a Moderate rule set (cases 12–27) — per-dimension determinism:
///   (a) RecurringError → forced Review even at Medium severity.
///   (b) Fatigue → smaller item set (nearer 3).
///   (c) Difficulty nudge bounded, never crosses band, only on Review items.
///   (d) Ordering: recurring-error item first within equal severity.
///   (e) Confidence gate per DataPointCount band.
///   - Soft passthrough: PreferredExplanationStyle set at full confidence, null otherwise.
///   - Reproducibility: same inputs → same output across all bands.
///   - Cold-start unchanged (DataPointCount == 0 → Celebrate).
/// </summary>
public sealed class RecommendationEngineTests
{
    // ── Helpers ──────────────────────────────────────────────────────────────────────────────────

    private static DerivedProfile ColdProfile() => new(
        QuestionTypeAffinity:      new Dictionary<string, double>(),
        RecurringErrorSkillIds:    Array.Empty<int>(),
        AttentionSpanMinutes:      null,
        PreferredExplanationStyle: ExplanationStyle.Standard,
        DataPointCount:            0,
        GritScore:                 null,
        MasteryVelocity:           null);

    /// <summary>Rich profile: DataPointCount >= ConfidenceDataPointThreshold (10), no fatigue, no recurring errors.</summary>
    private static DerivedProfile RichProfile() => new(
        QuestionTypeAffinity:      new Dictionary<string, double> { ["MCQ"] = 0.8 },
        RecurringErrorSkillIds:    Array.Empty<int>(),
        AttentionSpanMinutes:      null,
        PreferredExplanationStyle: ExplanationStyle.Standard,
        DataPointCount:            50,
        GritScore:                 null,
        MasteryVelocity:           null);

    /// <summary>
    /// Rich profile with specified recurring-error SkillIds — DataPointCount = 50 (full confidence).
    /// </summary>
    private static DerivedProfile RichProfileWithRecurringErrors(params int[] skillIds) => new(
        QuestionTypeAffinity:      new Dictionary<string, double>(),
        RecurringErrorSkillIds:    skillIds,
        AttentionSpanMinutes:      null,
        PreferredExplanationStyle: ExplanationStyle.Simplified,
        DataPointCount:            50,
        GritScore:                 null,
        MasteryVelocity:           null);

    /// <summary>
    /// Full-confidence profile with AttentionSpanMinutes ≤ fatigue threshold (20 min default).
    /// </summary>
    private static DerivedProfile FatiguedProfile(int attentionSpanMinutes = 15) => new(
        QuestionTypeAffinity:      new Dictionary<string, double>(),
        RecurringErrorSkillIds:    Array.Empty<int>(),
        AttentionSpanMinutes:      attentionSpanMinutes,
        PreferredExplanationStyle: ExplanationStyle.Simplified,
        DataPointCount:            50,
        GritScore:                 null,
        MasteryVelocity:           null);

    /// <summary>
    /// Full-confidence profile with AttentionSpanMinutes ≤ fatigue threshold AND recurring-error skills.
    /// </summary>
    private static DerivedProfile FatiguedProfileWithRecurring(int attentionSpanMinutes = 15, params int[] recurringSkillIds) => new(
        QuestionTypeAffinity:      new Dictionary<string, double>(),
        RecurringErrorSkillIds:    recurringSkillIds,
        AttentionSpanMinutes:      attentionSpanMinutes,
        PreferredExplanationStyle: ExplanationStyle.Simplified,
        DataPointCount:            50,
        GritScore:                 null,
        MasteryVelocity:           null);

    /// <summary>
    /// Low-confidence profile: 0 &lt; DataPointCount &lt; ConfidenceDataPointThreshold (10).
    /// </summary>
    private static DerivedProfile LowConfidenceProfile(params int[] recurringSkillIds) => new(
        QuestionTypeAffinity:      new Dictionary<string, double>(),
        RecurringErrorSkillIds:    recurringSkillIds,
        AttentionSpanMinutes:      10,
        PreferredExplanationStyle: ExplanationStyle.Simplified,
        DataPointCount:            5,   // 0 < 5 < 10 threshold
        GritScore:                 null,
        MasteryVelocity:           null);

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

    /// <summary>Default options (matches appsettings.json defaults).</summary>
    private static RecommendationOptions DefaultOpts() => new();

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // ── P5-09 original tests (1–11) — UNCHANGED ──────────────────────────────────────────────
    // ═════════════════════════════════════════════════════════════════════════════════════════════

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

    // ═════════════════════════════════════════════════════════════════════════════════════════════
    // ── P5-09a tests (12–27) — per-dimension Moderate rule set ───────────────────────────────
    // ═════════════════════════════════════════════════════════════════════════════════════════════

    // ── Rule (a): RecurringError → forced Review ──────────────────────────────────────────────

    [Fact]
    public void Compute_RecurringErrorSkill_ForcesReviewEvenAtMediumSeverity()
    {
        // Rule (a): SkillId 2 is a recurring-error skill with Medium severity → must become Review.
        var areas   = new[] { MediumArea(skillId: 2) };
        var profile = RichProfileWithRecurringErrors(2);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review,
            "recurring-error skill at Medium severity must be forced to Review (rule a)");
    }

    [Fact]
    public void Compute_RecurringErrorSkill_ForcesReviewAtHighSeverity()
    {
        // Rule (a): high severity recurring-error skill → still Review (no change from baseline).
        var areas   = new[] { HighArea(skillId: 1) };
        var profile = RichProfileWithRecurringErrors(1);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review);
    }

    [Fact]
    public void Compute_NonRecurringErrorMediumSkill_StaysPractice()
    {
        // SkillId 2 is NOT in recurring errors → Medium stays Practice.
        var areas   = new[] { MediumArea(skillId: 2) };
        var profile = RichProfileWithRecurringErrors(99);  // only skillId=99 is recurring

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice,
            "non-recurring-error Medium skill must remain Practice");
    }

    [Fact]
    public void Compute_RecurringErrorRule_IsDetachableFromSeverity_AndDeterministic()
    {
        // Reproducibility: rule (a) produces same output across two calls.
        var areas   = new[] { MediumArea(skillId: 5) };
        var profile = RichProfileWithRecurringErrors(5);

        var first  = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);
        var second = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        first.Should().BeEquivalentTo(second, "rule (a) must be deterministic");
        first[0].ActionType.Should().Be(RecommendationActionType.Review);
    }

    // ── Rule (b): Fatigue → smaller item set ──────────────────────────────────────────────────

    [Fact]
    public void Compute_FatiguedProfile_CapsSetAtFatigueQuantityCap()
    {
        // Rule (b): AttentionSpanMinutes = 15 (≤ 20 threshold) → cap at FatigueQuantityCap = 3.
        var areas = Enumerable.Range(1, 8)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();
        var profile = FatiguedProfile(attentionSpanMinutes: 15);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result.Length.Should().BeLessOrEqualTo(3, "fatigue-prone child should get ≤ 3 recommendations (rule b)");
    }

    [Fact]
    public void Compute_NonFatiguedProfile_AllowsFullCap()
    {
        // Non-fatigue child: AttentionSpanMinutes = null → allow up to MaxItems = 5.
        var areas = Enumerable.Range(1, 8)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();

        var result = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        result.Length.Should().Be(5, "non-fatigue child gets up to MaxItems = 5 recommendations");
    }

    [Fact]
    public void Compute_FatigueAtExactThreshold_IsConsideredFatigued()
    {
        // Boundary: AttentionSpanMinutes == threshold (20) → treated as fatigue-prone (≤ threshold).
        var areas = Enumerable.Range(1, 8)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();
        var profile = FatiguedProfile(attentionSpanMinutes: 20);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result.Length.Should().BeLessOrEqualTo(3, "AttentionSpanMinutes at exactly the threshold counts as fatigue-prone");
    }

    [Fact]
    public void Compute_FatigueAboveThreshold_NotFatigued()
    {
        // AttentionSpanMinutes = 21 (> 20) → not fatigue-prone → allows up to MaxItems = 5.
        var areas = Enumerable.Range(1, 8)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();
        var profile = new DerivedProfile(
            QuestionTypeAffinity:      new Dictionary<string, double>(),
            RecurringErrorSkillIds:    Array.Empty<int>(),
            AttentionSpanMinutes:      21,   // above threshold
            PreferredExplanationStyle: ExplanationStyle.Standard,
            DataPointCount:            50,
            GritScore:                 null,
            MasteryVelocity:           null);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result.Length.Should().Be(5, "AttentionSpanMinutes above threshold is not fatigue-prone");
    }

    // ── Rule (c): Difficulty nudge bounded, only on Review items ─────────────────────────────

    [Fact]
    public void Compute_DifficultyNudge_ReducesHardToMediumOnReviewForFatiguedChild()
    {
        // Rule (c): fatigue-prone child, High-severity area → Review; adaptivity says Hard → nudge to Medium.
        var areas     = new[] { HighArea(skillId: 1) };
        var decisions = DecisionForSkill(1, DifficultyLevel.Hard);
        var profile   = FatiguedProfile(attentionSpanMinutes: 15);

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review);
        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Medium,
            "Hard nudged to Medium for fatigue-prone child on Review item (rule c)");
    }

    [Fact]
    public void Compute_DifficultyNudge_ReducesMediumToEasyOnReviewForFatiguedChild()
    {
        // Rule (c): Medium difficulty Review item → nudge to Easy.
        var areas     = new[] { HighArea(skillId: 1) };
        var decisions = DecisionForSkill(1, DifficultyLevel.Medium);
        var profile   = FatiguedProfile(attentionSpanMinutes: 10);

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review);
        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Easy,
            "Medium nudged to Easy for fatigue-prone child on Review item (rule c)");
    }

    [Fact]
    public void Compute_DifficultyNudge_EasyRemainsEasyOnReviewForFatiguedChild()
    {
        // Rule (c): Easy is already the floor — no further nudge.
        var areas     = new[] { HighArea(skillId: 1) };
        var decisions = DecisionForSkill(1, DifficultyLevel.Easy);
        var profile   = FatiguedProfile(attentionSpanMinutes: 10);

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null);

        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Easy,
            "Easy stays Easy — nudge never goes below the floor (rule c bounded)");
    }

    [Fact]
    public void Compute_DifficultyNudge_NotAppliedOnPracticeItems()
    {
        // Rule (c): nudge only on Review items — Practice items keep original difficulty.
        var areas     = new[] { MediumArea(skillId: 2) };
        var decisions = DecisionForSkill(2, DifficultyLevel.Hard);
        var profile   = FatiguedProfile(attentionSpanMinutes: 10);

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice);
        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Hard,
            "difficulty nudge must NOT apply to non-Review items (rule c scope)");
    }

    [Fact]
    public void Compute_DifficultyNudgeDisabled_ReviewItemKeepsOriginalDifficulty()
    {
        // DifficultyNudgeEnabled = false → no nudge even for fatigue-prone child.
        var areas     = new[] { HighArea(skillId: 1) };
        var decisions = DecisionForSkill(1, DifficultyLevel.Hard);
        var profile   = FatiguedProfile(attentionSpanMinutes: 10);
        var opts      = new RecommendationOptions { DifficultyNudgeEnabled = false };

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null, options: opts);

        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Hard,
            "when DifficultyNudgeEnabled=false, no nudge is applied");
    }

    [Fact]
    public void Compute_DifficultyNudge_NeverCrossesAdaptivityBand_NonFatiguedChild()
    {
        // Rule (c): nudge only applies when isFatigued=true. Non-fatigue child → original difficulty preserved.
        var areas     = new[] { HighArea(skillId: 1) };
        var decisions = DecisionForSkill(1, DifficultyLevel.Hard);
        // Non-fatigued: AttentionSpanMinutes = null
        var profile = RichProfile();

        var result = RecommendationEngine.Compute(areas, decisions, profile, grade: null);

        result[0].TargetDifficulty.Should().Be((int)DifficultyLevel.Hard,
            "nudge must not apply for non-fatigue child — adaptivity band must not be crossed");
    }

    // ── Rule (d): Ordering — recurring-error first within equal severity ──────────────────────

    [Fact]
    public void Compute_RecurringErrorItem_SurfacesFirstWithinEqualSeverity()
    {
        // Rule (d): two Medium-severity items; skillId=10 is a recurring-error → must come first.
        var areas = new[]
        {
            MediumArea(skillId: 20, masteryPct: 35),   // higher mastery deficit, not recurring
            MediumArea(skillId: 10, masteryPct: 40),   // lower mastery deficit, BUT recurring
        };
        var profile = RichProfileWithRecurringErrors(10);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].SkillId.Should().Be(10, "recurring-error skill must surface first within equal severity (rule d)");
        result[1].SkillId.Should().Be(20);
    }

    [Fact]
    public void Compute_HighSeverityAlwaysBeforeMediumRecurringError()
    {
        // Severity is still the primary sort: a High-severity non-recurring item beats
        // a Medium-severity recurring-error item.
        var areas = new[]
        {
            MediumArea(skillId: 5, masteryPct: 30),   // recurring
            HighArea(skillId: 1,   masteryPct: 15),   // not recurring
        };
        var profile = RichProfileWithRecurringErrors(5);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].SkillId.Should().Be(1, "High severity must still beat Medium recurring error (severity first)");
    }

    // ── Rule (e): Confidence gate per DataPointCount band ────────────────────────────────────

    [Fact]
    public void Compute_DataPointCountZero_ColdStartBehaviourUnchanged()
    {
        // DataPointCount == 0 → cold-start: Low → Celebrate (unchanged P5-09 behaviour).
        var areas   = new[] { LowArea(skillId: 3) };
        var profile = ColdProfile();  // DataPointCount = 0

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Celebrate,
            "DataPointCount==0 cold-start must remain unchanged (Low→Celebrate)");
    }

    [Fact]
    public void Compute_DataPointCountZero_NoProfileRulesApplied()
    {
        // DataPointCount == 0 → cold-start: recurring-error rule (a) must NOT apply.
        var areas   = new[] { MediumArea(skillId: 2) };
        var profile = new DerivedProfile(
            QuestionTypeAffinity:      new Dictionary<string, double>(),
            RecurringErrorSkillIds:    new[] { 2 },  // skillId 2 is recurring
            AttentionSpanMinutes:      10,
            PreferredExplanationStyle: ExplanationStyle.Simplified,
            DataPointCount:            0,             // cold-start
            GritScore:                 null,
            MasteryVelocity:           null);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice,
            "DataPointCount==0: rule (a) must NOT fire — cold-start behaviour unchanged");
    }

    [Fact]
    public void Compute_LowConfidence_OnlyRecurringErrorRuleApplied()
    {
        // 0 < DataPointCount < threshold (5 < 10): rule (a) applies — recurring Medium → Review.
        var areas   = new[] { MediumArea(skillId: 2) };
        var profile = LowConfidenceProfile(2);  // skillId=2 is recurring, DataPointCount=5

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Review,
            "low confidence (0<DataPointCount<threshold): rule (a) must still apply");
    }

    [Fact]
    public void Compute_LowConfidence_FatigueRuleNotApplied()
    {
        // 0 < DataPointCount < threshold: rule (b) fatigue cap must NOT apply.
        var areas = Enumerable.Range(1, 8)
            .Select(i => new WeakAreaEntry(i, $"Skill{i}", 0, 30 + i, WeakAreaSeverity.Medium, "PracticeSkill"))
            .ToArray();
        var profile = LowConfidenceProfile();  // DataPointCount=5, AttentionSpanMinutes=10 (below threshold)

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result.Length.Should().BeGreaterThan(3,
            "low confidence: fatigue cap (rule b) must NOT apply — only rule (a) is active at low confidence");
    }

    [Fact]
    public void Compute_LowConfidence_NonRecurringMediumStaysPractice()
    {
        // Low confidence: skillId=2 is NOT in recurring errors → stays Practice.
        var areas   = new[] { MediumArea(skillId: 2) };
        var profile = LowConfidenceProfile(99);  // only skillId=99 is recurring

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].ActionType.Should().Be(RecommendationActionType.Practice,
            "low confidence, non-recurring skill: grade+mastery mapping (Medium→Practice) applies");
    }

    // ── PreferredExplanationStyle soft passthrough ────────────────────────────────────────────

    [Fact]
    public void Compute_FullProfile_ExplanationStyleIsPropagatedToItem()
    {
        // Full confidence profile → PreferredExplanationStyle is set on the item (soft passthrough).
        var areas   = new[] { MediumArea() };
        var profile = new DerivedProfile(
            QuestionTypeAffinity:      new Dictionary<string, double>(),
            RecurringErrorSkillIds:    Array.Empty<int>(),
            AttentionSpanMinutes:      null,
            PreferredExplanationStyle: ExplanationStyle.Simplified,
            DataPointCount:            50,
            GritScore:                 null,
            MasteryVelocity:           null);

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].PreferredExplanationStyle.Should().Be(RecommendationExplanationStyle.Simplified,
            "full-confidence profile must propagate PreferredExplanationStyle to the item");
    }

    [Fact]
    public void Compute_ColdStartProfile_ExplanationStyleIsNull()
    {
        // DataPointCount == 0 → PreferredExplanationStyle must be null on items.
        var areas   = new[] { LowArea() };
        var profile = ColdProfile();  // DataPointCount = 0

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].PreferredExplanationStyle.Should().BeNull(
            "cold-start (DataPointCount==0) items must have null PreferredExplanationStyle");
    }

    [Fact]
    public void Compute_LowConfidenceProfile_ExplanationStyleIsNull()
    {
        // 0 < DataPointCount < threshold → PreferredExplanationStyle must be null on items.
        var areas   = new[] { MediumArea() };
        var profile = LowConfidenceProfile();  // DataPointCount = 5

        var result = RecommendationEngine.Compute(areas, NoDecisions(), profile, grade: null);

        result[0].PreferredExplanationStyle.Should().BeNull(
            "low-confidence items must have null PreferredExplanationStyle (soft passthrough only at full confidence)");
    }

    // ── Reproducibility across all bands ─────────────────────────────────────────────────────

    [Fact]
    public void Compute_FullProfileRules_ProduceSameOutputOnRepeatedCall()
    {
        // Reproducibility: full moderate rule set, same inputs → identical output each time.
        var areas = new[]
        {
            MediumArea(skillId: 5, masteryPct: 35),   // recurring
            MediumArea(skillId: 7, masteryPct: 38),   // not recurring
            HighArea(skillId: 1,   masteryPct: 15),
        };
        var profile = FatiguedProfileWithRecurring(attentionSpanMinutes: 10, 5);
        var decisions = DecisionForSkill(1, DifficultyLevel.Hard);

        var first  = RecommendationEngine.Compute(areas, decisions, profile, grade: 3);
        var second = RecommendationEngine.Compute(areas, decisions, profile, grade: 3);

        first.Should().BeEquivalentTo(second, "full profile moderate rules must be deterministic");
    }

    // ── Un-conflation: gamification level not in engine ──────────────────────────────────────

    [Fact]
    public void Compute_GamificationLevel_IsNotAParameterAndHasNoEffect()
    {
        // Engine signature has no gamification-level parameter — rule must be enforced by convention.
        // We assert that identical results are produced regardless of any framing concern:
        // grade=null runs as well as grade=5 (grade is scope/tone, not ranking).
        var areas  = new[] { HighArea(), MediumArea() };

        var withGrade    = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: 5);
        var withoutGrade = RecommendationEngine.Compute(areas, NoDecisions(), RichProfile(), grade: null);

        // Output shape (skill IDs, severities, action types, difficulties) must be the same.
        withGrade.Select(i => i.SkillId).Should().BeEquivalentTo(
            withoutGrade.Select(i => i.SkillId),
            "grade must not affect ranking or action-type selection (un-conflation rule)");
        withGrade.Select(i => i.ActionType).Should().BeEquivalentTo(
            withoutGrade.Select(i => i.ActionType),
            "grade must not affect action types (un-conflation rule)");
    }
}
