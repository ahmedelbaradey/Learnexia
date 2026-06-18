using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Domain.Services;
using Learnexia.Shared.Contracts.Learning;
using Resources;

namespace Learnexia.Modules.Learning.Application.Services;

/// <summary>
/// Pure static application service for computing a ranked, deterministic recommendation set (P5-09 / P5-09a).
///
/// CONTRACT:
/// - Pure static — no database, no DI, no I/O. The caller (<c>RecommendationService</c>)
///   pre-fetches all inputs and passes them here.
/// - ONE plain static class. No Strategy / Visitor / Pipeline / State patterns (CLAUDE rule 8).
/// - Thread-safe: no shared mutable state.
/// - Deterministic and reproducible: same inputs always produce the same output (P5-09 AC1).
/// - Rule-based and explainable: every item can be traced back to the three input signals
///   and/or a named <see cref="DerivedProfile"/> dimension (P5-09a explainability AC).
///
/// THREE UN-CONFLATED SIGNALS (per brief/spec, locked):
///   1. <b>Grade</b> (from Identity via <c>IChildGradeQuery</c>) — curriculum scope guard.
///      Currently unused in ranking; stored for the Lexi narration tier (P3-14 tone).
///   2. <b>Mastery / weak areas</b> (<c>IWeakAreaDetectorService</c>) — drives WHICH areas
///      appear and their severity ranking.
///   3. <b>AdaptivityEngine</b> (<c>IAdaptivityService.GetTargetDifficulty</c> per skill) —
///      drives <see cref="RecommendationItem.TargetDifficulty"/> per item.
///   Gamification level → NOT used here (explicitly excluded per spec — it is Lexi's framing signal).
///
/// PROFILE RULES (P5-09a — Moderate rule set, gated by DataPointCount confidence):
///   (a) RecurringErrorSkillIds: area whose SkillId is in recurring errors → forced Review
///       even at Medium severity (practice alone hasn't worked).
///   (b) AttentionSpanMinutes fatigue: ≤ threshold → cap item set nearer 3 (not 5) so the
///       child is not overwhelmed.
///   (c) DifficultyNudge (DifficultyNudgeEnabled): for fatigue-prone children, Review items
///       nudge toward the lower edge of the adaptivity band — Medium → Easy, Hard → Medium.
///       NEVER crosses the band; GetTargetDifficulty stays source of truth for non-nudge paths.
///   (d) Ordering: recurring-error item first within equal severity.
///   (e) Confidence gate: DataPointCount == 0 → unchanged cold-start; 0 < DataPointCount
///       < ConfidenceDataPointThreshold → rule (a) only, else grade + mastery; full profile
///       rules apply at DataPointCount >= ConfidenceDataPointThreshold.
///   PreferredExplanationStyle: soft passthrough only — never a hard driver for action/difficulty.
///
/// COLD-START / EMPTY:
///   When <paramref name="weakAreas"/> is empty, returns a well-formed encouraging set
///   (one <see cref="RecommendationActionType.Celebrate"/> item) — never an error.
///
/// OUTPUT CAP: 3–5 items (spec max 5, cold-start minimum 1).
///
/// RANKING RULE: severity descending → mastery-deficit descending (most-stuck first).
/// Within equal severity, recurring-error items surface first (rule d).
///
/// PATTERN CAUTION (rule 8):
///   The ranking/mapping logic lives as private methods in this one class. Do NOT refactor
///   into strategy classes or separate abstractions without lead approval.
/// </summary>
public static class RecommendationEngine
{
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
    /// Enriches action-type selection, quantity cap, difficulty-nudge, and ordering.
    /// </param>
    /// <param name="grade">
    /// The student's current grade (from <c>IChildGradeQuery</c>).
    /// Null = unknown; engine still runs — grade is not a hard dependency in the core ranking.
    /// </param>
    /// <param name="options">
    /// Tunable thresholds for the recommendation engine (resolved from
    /// <c>IOptions&lt;RecommendationOptions&gt;</c>). Defaults are safe v1 values.
    /// </param>
    /// <returns>
    /// A ranked, capped (1–5) <see cref="RecommendationItem"/> array.
    /// Never null, never empty (cold-start set is returned when no weak areas exist).
    /// </returns>
    public static RecommendationItem[] Compute(
        IReadOnlyList<WeakAreaEntry> weakAreas,
        IReadOnlyDictionary<int, AdaptivityDecision> adaptivityDecisions,
        DerivedProfile profile,
        int? grade,
        RecommendationOptions? options = null)
    {
        var opts = options ?? new RecommendationOptions();

        if (weakAreas.Count == 0)
            return BuildColdStartSet();

        // ── Confidence gate (P5-09a rule e) ────────────────────────────────────────────────────────
        // DataPointCount == 0 → pure cold-start (unchanged): return grade+mastery sorted set, no profile rules.
        // 0 < DataPointCount < threshold → low-confidence: apply rule (a) only (RecurringError→Review).
        // DataPointCount >= threshold → full moderate rule set applies.
        var dataPointCount = profile.DataPointCount;
        var isFullProfile  = dataPointCount >= opts.ConfidenceDataPointThreshold;
        var isLowConfidence = dataPointCount > 0 && dataPointCount < opts.ConfidenceDataPointThreshold;
        // Note: dataPointCount == 0 → isFullProfile=false AND isLowConfidence=false → cold-start path below.

        // ── Determine quantity cap ──────────────────────────────────────────────────────────────────
        // Rule (b): low AttentionSpanMinutes → cap nearer 3. Only applied with full profile confidence.
        var isFatigued = isFullProfile
            && profile.AttentionSpanMinutes.HasValue
            && profile.AttentionSpanMinutes.Value <= opts.AttentionSpanFatigueThresholdMinutes;

        var quantityCap = isFatigued
            ? Math.Max(opts.MinItems, opts.FatigueQuantityCap)   // fatigue cap, but never below MinItems
            : opts.MaxItems;                                      // normal cap

        // ── Build recurring-error id set ───────────────────────────────────────────────────────────
        // Rule (a) applies at BOTH full-profile and low-confidence bands (it is the most conservative
        // rule and is explicitly included in low-confidence mode per AC / confidence-gate spec).
        // Rule (d) ordering applies at full-profile only.
        var recurringErrorIds = (isFullProfile || isLowConfidence)
            ? new HashSet<int>(profile.RecurringErrorSkillIds)
            : new HashSet<int>();

        var sorted = weakAreas
            .OrderByDescending(w => (int)w.Severity)
            .ThenByDescending(w => 100 - w.MasteryPercent)
            // Rule (d): surface recurring-error items first within equal severity.
            // Applied as a secondary stable sort within same-severity bucket.
            .ToList();

        if (isFullProfile && recurringErrorIds.Count > 0)
        {
            // Stable-sort: within equal severity, recurring-error items bubble to front.
            sorted = sorted
                .OrderByDescending(w => (int)w.Severity)
                .ThenByDescending(w => recurringErrorIds.Contains(w.SkillId) ? 1 : 0)
                .ThenByDescending(w => 100 - w.MasteryPercent)
                .ToList();
        }

        sorted = sorted.Take(quantityCap).ToList();

        var items = new List<RecommendationItem>(sorted.Count);

        foreach (var area in sorted)
        {
            var difficulty = ResolveTargetDifficulty(area.SkillId, adaptivityDecisions);
            var actionType = ResolveActionType(area.SkillId, area.Severity, profile, isFullProfile, isLowConfidence, recurringErrorIds);

            // Rule (c): bounded difficulty nudge toward lower band edge on Review items for fatigue-prone children.
            // Only when DifficultyNudgeEnabled, and only on Review action items.
            if (opts.DifficultyNudgeEnabled && isFatigued && actionType == RecommendationActionType.Review)
                difficulty = NudgeDifficultyDown(difficulty);

            var (titleKey, bodyKey, ctaKey) = ResolveKeys(actionType);

            // Rule: PreferredExplanationStyle — soft passthrough only (never a hard driver).
            // Only set when full profile confidence; null on cold-start or low-confidence.
            var explanationStyle = isFullProfile
                ? MapExplanationStyle(profile.PreferredExplanationStyle)
                : (RecommendationExplanationStyle?)null;

            items.Add(new RecommendationItem(
                SkillId:                    area.SkillId,
                SubjectCode:                area.SubjectCode,
                TitleKey:                   titleKey,
                BodyKey:                    bodyKey,
                CtaKey:                     ctaKey,
                Severity:                   (int)area.Severity,
                ActionType:                 actionType,
                TargetDifficulty:           (int)difficulty,
                PreferredExplanationStyle:  explanationStyle));
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
    /// MODERATE RULE SET (P5-09a):
    ///   Full profile confidence (DataPointCount >= threshold):
    ///     Rule (a): SkillId in RecurringErrorSkillIds → always Review (even at Medium severity).
    ///     High severity → Review.
    ///     Medium severity → Practice.
    ///     Low severity + DataPointCount == 0 → Celebrate.
    ///     Low severity + DataPointCount > 0  → Practice.
    ///
    ///   Low confidence (0 < DataPointCount < threshold):
    ///     Rule (a) only: RecurringError → Review. Otherwise grade+mastery rules.
    ///
    ///   Cold-start (DataPointCount == 0):
    ///     Unchanged: High→Review, Medium→Practice, Low→Celebrate.
    /// </summary>
    private static RecommendationActionType ResolveActionType(
        int skillId,
        WeakAreaSeverity severity,
        DerivedProfile profile,
        bool isFullProfile,
        bool isLowConfidence,
        HashSet<int> recurringErrorIds)
    {
        // Rule (a): recurring-error skill → force Review regardless of severity.
        // Applied at both full-profile and low-confidence bands (most conservative rule — AC).
        if ((isFullProfile || isLowConfidence) && recurringErrorIds.Contains(skillId))
            return RecommendationActionType.Review;

        // Standard severity-based mapping (cold-start and low-confidence fallback).
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
    /// Bounded difficulty nudge toward the lower edge of the adaptivity band (P5-09a rule c).
    ///
    /// Applied ONLY on <see cref="RecommendationActionType.Review"/> items for fatigue-prone children.
    /// The nudge steps difficulty one level down (Hard → Medium, Medium → Easy, Easy → Easy).
    /// It NEVER crosses the AdaptivityEngine band — <c>GetTargetDifficulty</c> remains the source
    /// of truth; this is a bounded hint, not a recompute.
    /// </summary>
    private static DifficultyLevel NudgeDifficultyDown(DifficultyLevel difficulty) =>
        difficulty switch
        {
            DifficultyLevel.Hard   => DifficultyLevel.Medium,
            DifficultyLevel.Medium => DifficultyLevel.Easy,
            DifficultyLevel.Easy   => DifficultyLevel.Easy,   // already at floor — no change
            _                      => DifficultyLevel.Medium,
        };

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

    /// <summary>
    /// Maps the Domain <see cref="ExplanationStyle"/> enum value (from <c>DerivedProfile</c>) to
    /// the contract-layer <see cref="RecommendationExplanationStyle"/> enum (in <c>Shared.Contracts</c>).
    ///
    /// Both enums share integer values intentionally — this cast is safe, but the explicit mapping
    /// makes the mirroring contract visible and survives if either side diverges.
    /// </summary>
    private static RecommendationExplanationStyle MapExplanationStyle(ExplanationStyle style) =>
        style switch
        {
            ExplanationStyle.Standard   => RecommendationExplanationStyle.Standard,
            ExplanationStyle.Simplified => RecommendationExplanationStyle.Simplified,
            ExplanationStyle.Visual     => RecommendationExplanationStyle.Visual,
            ExplanationStyle.StepByStep => RecommendationExplanationStyle.StepByStep,
            _                           => RecommendationExplanationStyle.Standard,
        };
}
