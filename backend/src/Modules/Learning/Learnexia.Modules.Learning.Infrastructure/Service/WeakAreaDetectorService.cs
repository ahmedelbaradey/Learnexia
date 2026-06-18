using Learnexia.Modules.Learning.Application.Abstractions;
using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Service;

/// <summary>
/// Infrastructure implementation of <see cref="IWeakAreaDetectorService"/> (P5-02-BE-1).
///
/// <para>Algorithm (mirror existing mastery shapes — no new design patterns):</para>
/// <list type="number">
///   <item>Load all <c>StudentSkillMastery</c> rows for the student joined to
///         <c>Skill → Concept → Subject</c> so we know the subject code for each skill.</item>
///   <item>For each row, compute severity:
///     <list type="bullet">
///       <item><see cref="WeakAreaSeverity.High"/>   — mastery &lt; 30 %.</item>
///       <item><see cref="WeakAreaSeverity.Medium"/> — 30 % ≤ mastery &lt; 50 %
///             (<c>NeedsReview</c> range).</item>
///       <item><see cref="WeakAreaSeverity.Low"/>    — mastery ≥ 50 % <em>but</em> the student's
///             most-recent completed <c>Attempt</c> for this skill scores &lt; 60 % accuracy.</item>
///     </list>
///     Skills with <c>NotStarted</c> or mastery ≥ 50 % whose recent accuracy ≥ 60 % are
///     <em>not</em> weak areas.</item>
///   <item>Rank: High → Medium → Low, then by mastery deficit descending within each tier.</item>
///   <item>Apply <paramref name="maxResults"/> cap if &gt; 0.</item>
/// </list>
///
/// <para>Empty / first-week safety: returns empty list (never null) when no weak areas exist.
/// All EF reads are <c>AsNoTracking</c>; this service is read-only.</para>
/// </summary>
public sealed class WeakAreaDetectorService : IWeakAreaDetectorService
{
    // Threshold for the "Low" severity tier: mastery ≥ 50% but recent accuracy below this.
    private const double LowAccuracyThreshold = 60.0;

    // Mastery thresholds for High / Medium severity.
    private const int HighSeverityMaxMastery   = 30;
    private const int MediumSeverityMaxMastery = 50;

    private readonly LearningDbContext _db;

    public WeakAreaDetectorService(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<IReadOnlyList<WeakAreaEntry>> DetectAsync(
        int studentId,
        int maxResults = 5,
        CancellationToken ct = default)
    {
        // ── Step 1: Load all mastery rows with skill→concept→subject chain ──────────
        var masteryRows = await _db.StudentSkillMasteries
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && m.Status != MasteryStatus.NotStarted)
            .Select(m => new
            {
                m.SkillId,
                SkillName       = m.Skill.Name,
                SubjectCode     = m.Skill.Concept.Subject.SubjectCode,
                m.MasteryPercentage,
                m.Status,
                m.AttemptsCount,
            })
            .ToListAsync(ct);

        if (masteryRows.Count == 0)
            return Array.Empty<WeakAreaEntry>();

        // ── Step 2: For the "Low" tier we need recent attempt accuracy ────────────
        // Collect the set of skillIds that are candidates for the Low tier
        // (mastery >= 50, i.e. InProgress or Mastered) — only these need the accuracy lookup.
        var lowCandidateSkillIds = masteryRows
            .Where(m => m.MasteryPercentage >= MediumSeverityMaxMastery)
            .Select(m => m.SkillId)
            .ToHashSet();

        // Fetch the most-recent completed attempt accuracy per skill via StudentAnswer → QuizQuestion.
        // We look at the most recent completed Attempt that touched each skill via answered questions.
        Dictionary<int, double> recentAccuracyBySkill = new();
        if (lowCandidateSkillIds.Count > 0)
        {
            // For each skill, get the AccuracyPercentage of the most recently completed attempt
            // that contained a question for that skill.
            var recentAccuracies = await _db.StudentAnswers
                .AsNoTracking()
                .Where(sa =>
                    sa.Attempt.StudentId == studentId
                    && sa.Attempt.Status == AttemptStatus.Completed
                    && sa.Question.SkillId.HasValue
                    && lowCandidateSkillIds.Contains(sa.Question.SkillId!.Value))
                .GroupBy(sa => sa.Question.SkillId!.Value)
                .Select(g => new
                {
                    SkillId = g.Key,
                    // Pick accuracy from the attempt with the latest CompletedAt.
                    RecentAccuracy = g
                        .OrderByDescending(sa => sa.Attempt.CompletedAt)
                        .Select(sa => sa.Attempt.AccuracyPercentage)
                        .FirstOrDefault(),
                })
                .ToListAsync(ct);

            recentAccuracyBySkill = recentAccuracies.ToDictionary(r => r.SkillId, r => r.RecentAccuracy);
        }

        // ── Step 3: Score each mastery row ─────────────────────────────────────────
        var weakEntries = new List<(WeakAreaEntry Entry, int SeverityOrder, int Deficit)>();

        foreach (var m in masteryRows)
        {
            WeakAreaSeverity? severity = null;

            if (m.MasteryPercentage < HighSeverityMaxMastery)
            {
                severity = WeakAreaSeverity.High;
            }
            else if (m.MasteryPercentage < MediumSeverityMaxMastery)
            {
                severity = WeakAreaSeverity.Medium;
            }
            else
            {
                // Mastery ≥ 50% — only weak if recent accuracy < threshold.
                if (recentAccuracyBySkill.TryGetValue(m.SkillId, out var recentAcc)
                    && recentAcc < LowAccuracyThreshold)
                {
                    severity = WeakAreaSeverity.Low;
                }
            }

            if (severity is null)
                continue;

            // SuggestedNextAction: a fixed enum-backed key, not a free-text string.
            // Mapped to a SharedResourcesKey constant consumed by the caller.
            var actionKey = severity == WeakAreaSeverity.High
                ? SuggestedActionKeys.ReviewConcept
                : SuggestedActionKeys.PracticeSkill;

            var entry = new WeakAreaEntry(
                SkillId:           m.SkillId,
                SkillName:         m.SkillName,
                SubjectCode:       (int)m.SubjectCode,
                MasteryPercent:    m.MasteryPercentage,
                Severity:          severity.Value,
                SuggestedNextAction: actionKey);

            // Rank: High=3 → Medium=2 → Low=1 (descending), then deficit descending.
            int deficit = 100 - m.MasteryPercentage;
            weakEntries.Add((entry, (int)severity.Value, deficit));
        }

        // ── Step 4: Sort and cap ────────────────────────────────────────────────────
        var sorted = weakEntries
            .OrderByDescending(x => x.SeverityOrder)
            .ThenByDescending(x => x.Deficit)
            .Select(x => x.Entry);

        var result = maxResults > 0 ? sorted.Take(maxResults) : sorted;

        return result.ToList().AsReadOnly();
    }
}

/// <summary>
/// Fixed key constants for the SuggestedNextAction field on <see cref="WeakAreaEntry"/>.
/// These are non-user-facing technical identifiers (matching SharedResourcesKey constants
/// for the Parent module's localizer to resolve when displaying). Permitted literal per
/// CLAUDE.md rule: "non-user-facing technical identifiers".
/// </summary>
public static class SuggestedActionKeys
{
    /// <summary>Review the underlying concept (for High severity).</summary>
    public const string ReviewConcept = nameof(ReviewConcept);

    /// <summary>Practice the skill more (for Medium / Low severity).</summary>
    public const string PracticeSkill = nameof(PracticeSkill);
}
