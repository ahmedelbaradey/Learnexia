using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Resources;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// Pure static application service for computing a ranked, deterministic recommendation set (P5-09).
///
/// CONTRACT:
/// - Pure static — no database, no DI, no I/O. The caller (<c>RecommendationService</c>)
///   pre-fetches all inputs and passes them here.
/// - ONE plain static class. No Strategy / Visitor / Pipeline / State patterns (CLAUDE rule 8).
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: same inputs always produce the same output (P5-09 AC1).
/// - Rule-based and explainable: every item can be traced back to the three input signals.
///
/// THREE UN-CONFLATED SIGNALS (per brief/spec, locked):
///   1. <b>Grade</b> (from Identity via <c>IChildGradeQuery</c>) — curriculum scope guard.
///      Currently unused in ranking; stored for the Lexi narration tier (P3-14 tone).
///   2. <b>Mastery / weak areas</b> (<c>IWeakAreaDetectorService</c>) — drives WHICH areas
///      appear and their severity ranking.
///   3. <b>AdaptivityEngine</b> (<c>IAdaptivityService.GetTargetDifficulty</c> per skill) —
///      drives <see cref="RecommendationItem.TargetDifficulty"/> per item.
///   Gamification level → NOT used here (explicitly excluded per spec).
///
/// COLD-START / EMPTY:
///   When <paramref name="weakAreas"/> is empty, returns a well-formed encouraging set
///   (one <see cref="RecommendationActionType.Celebrate"/> item) — never an error.
///
/// OUTPUT CAP: 3–5 items (spec max 5, cold-start minimum 1).
///
/// RANKING RULE: severity descending → mastery-deficit descending (most-stuck first).
///
/// PATTERN CAUTION (rule 8):
///   The ranking/mapping logic lives as private methods in this one class. Do NOT refactor
///   into strategy classes or separate abstractions without lead approval.
/// </summary>
public static class RecommendationEngine
{
    private const int MaxItems = 5;

    /// <summary>
    /// Computes the deterministic recommendation set for a student.
    /// </summary>
    /// <param name="weakAreas">
    /// Ranked weak areas from <c>IWeakAreaDetectorService.DetectAsync</c>.
    /// Empty list = cold-start / no weak areas.
    /// </param>
    /// <param name="adaptivityDecisions">
    /// Pre-fetched per-skill <see cref="AdaptivityDecision"/> keyed by SkillId.
    /// Skills not present in this map default to <see cref="DifficultyLevel.Medium"/>.
    /// </param>
    /// <param name="profile">
    /// Derived behavioral profile from <c>IStudentProfileService.GetProfile</c>.
    /// Enriches action-type selection (cold-start profile → more encouraging items).
    /// </param>
    /// <param name="grade">
    /// The student's current grade (from <c>IChildGradeQuery</c>).
    /// Null = unknown; engine still runs — grade is not a hard dependency in the core ranking.
    /// </param>
    /// <returns>
    /// A ranked, capped (1–5) <see cref="RecommendationItem"/> array.
    /// Never null, never empty (cold-start set is returned when no weak areas exist).
    /// </returns>
    public static RecommendationItem[] Compute(
        IReadOnlyList<WeakAreaEntry> weakAreas,
        IReadOnlyDictionary<int, AdaptivityDecision> adaptivityDecisions,
        DerivedProfile profile,
        int? grade)
    {
        if (weakAreas.Count == 0)
            return BuildColdStartSet();

        // ── Sort: severity descending, then mastery deficit (100 - MasteryPercent) descending ──
        var sorted = weakAreas
            .OrderByDescending(w => (int)w.Severity)
            .ThenByDescending(w => 100 - w.MasteryPercent)
            .Take(MaxItems)
            .ToList();

        var items = new List<RecommendationItem>(sorted.Count);

        foreach (var area in sorted)
        {
            var difficulty = ResolveTargetDifficulty(area.SkillId, adaptivityDecisions);
            var actionType = ResolveActionType(area.Severity, profile);
            var (titleKey, bodyKey, ctaKey) = ResolveKeys(actionType);

            items.Add(new RecommendationItem(
                SkillId:          area.SkillId,
                SubjectCode:      area.SubjectCode,
                TitleKey:         titleKey,
                BodyKey:          bodyKey,
                CtaKey:           ctaKey,
                Severity:         (int)area.Severity,
                ActionType:       actionType,
                TargetDifficulty: (int)difficulty));
        }

        return items.ToArray();
    }

    // ─────────────────────────────────────────────────────────────────────────────────────────────
    // PRIVATE HELPERS — do NOT refactor into strategy classes without lead approval (rule 8).
    // ─────────────────────────────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns a well-formed encouraging set for a student with no weak areas.
    /// Always contains exactly one Celebrate item so the result is never empty.
    /// </summary>
    private static RecommendationItem[] BuildColdStartSet() =>
    [
        new RecommendationItem(
            SkillId:          0,
            SubjectCode:      0,
            TitleKey:         SharedResourcesKey.RecColdStartTitle,
            BodyKey:          SharedResourcesKey.RecColdStartBody,
            CtaKey:           SharedResourcesKey.RecColdStartCta,
            Severity:         (int)WeakAreaSeverity.Low,
            ActionType:       RecommendationActionType.Celebrate,
            TargetDifficulty: (int)DifficultyLevel.Medium)
    ];

    /// <summary>
    /// Resolves the target difficulty for a skill from the pre-fetched adaptivity decisions.
    /// Defaults to <see cref="DifficultyLevel.Medium"/> when the skill is not in the map
    /// (mirrors AdaptivityService cold-start behaviour).
    /// </summary>
    private static DifficultyLevel ResolveTargetDifficulty(
        int skillId,
        IReadOnlyDictionary<int, AdaptivityDecision> decisions)
    {
        if (decisions.TryGetValue(skillId, out var decision))
            return decision.Difficulty;

        return DifficultyLevel.Medium;
    }

    /// <summary>
    /// Chooses the <see cref="RecommendationActionType"/> from severity + profile signals.
    ///
    /// RULE:
    ///   High severity → Review (concept review first; student is deeply stuck).
    ///   Medium severity → Practice (practise the skill at target difficulty).
    ///   Low severity AND cold-start profile (DataPointCount == 0) → Celebrate (encouraging).
    ///   Low severity AND has data → Practice (drifting; practise to re-solidify).
    /// </summary>
    private static RecommendationActionType ResolveActionType(
        WeakAreaSeverity severity,
        DerivedProfile profile)
    {
        return severity switch
        {
            WeakAreaSeverity.High   => RecommendationActionType.Review,
            WeakAreaSeverity.Medium => RecommendationActionType.Practice,
            WeakAreaSeverity.Low    => profile.DataPointCount == 0
                                           ? RecommendationActionType.Celebrate
                                           : RecommendationActionType.Practice,
            _                       => RecommendationActionType.Practice,
        };
    }

    /// <summary>
    /// Maps an action type to its i18n title/body/CTA key triple.
    /// All keys are constants from <see cref="SharedResourcesKey"/> — no inline strings.
    /// </summary>
    private static (string titleKey, string bodyKey, string ctaKey) ResolveKeys(
        RecommendationActionType actionType)
    {
        return actionType switch
        {
            RecommendationActionType.Review     => (SharedResourcesKey.RecReviewTitle, SharedResourcesKey.RecReviewBody, SharedResourcesKey.RecReviewCta),
            RecommendationActionType.Practice   => (SharedResourcesKey.RecPracticeTitle, SharedResourcesKey.RecPracticeBody, SharedResourcesKey.RecPracticeCta),
            RecommendationActionType.KeepStreak => (SharedResourcesKey.RecPracticeTitle, SharedResourcesKey.RecPracticeBody, SharedResourcesKey.RecPracticeCta),
            RecommendationActionType.Celebrate  => (SharedResourcesKey.RecColdStartTitle, SharedResourcesKey.RecColdStartBody, SharedResourcesKey.RecColdStartCta),
            _                                   => (SharedResourcesKey.RecPracticeTitle, SharedResourcesKey.RecPracticeBody, SharedResourcesKey.RecPracticeCta),
        };
    }
}
