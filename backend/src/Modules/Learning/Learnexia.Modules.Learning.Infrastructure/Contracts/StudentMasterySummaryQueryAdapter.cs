using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IStudentMasterySummaryQuery"/> by querying <see cref="LearningDbContext"/>
/// for per-subject mastery summary required by the Parent analytics API (P5-08-BE-3).
///
/// <para>Cross-module isolation: the Parent module injects <see cref="IStudentMasterySummaryQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Learning project directly.</para>
///
/// <para>Algorithm:</para>
/// <list type="number">
///   <item>Load all <c>StudentSkillMastery</c> rows for the student joined to
///         <c>Skill → Concept → Subject.SubjectCode</c>.</item>
///   <item>Group by <c>SubjectCode</c>, average the <c>MasteryPercentage</c>
///         (integer truncation rounds down — mirrors <c>MasteryEngine.Compute</c> style).</item>
///   <item>For every MVP subject that has no rows, emit a 0% entry (sentinel).</item>
///   <item>Overall = average across all skill rows (zero when no rows).</item>
/// </list>
///
/// <para>Sentinel: all four subjects always present in the result — 0% for subjects with no data.</para>
/// </summary>
public sealed class StudentMasterySummaryQueryAdapter : IStudentMasterySummaryQuery
{
    // The four MVP SubjectCode int values (matches Learning.Domain.Enums.SubjectCode).
    // Defined here as int to avoid a cross-module enum reference back into Learning.Domain.
    private static readonly IReadOnlyList<int> AllSubjectCodes =
        new[] { (int)SubjectCode.MATH, (int)SubjectCode.SCIENCE, (int)SubjectCode.ARABIC, (int)SubjectCode.ENGLISH };

    private readonly LearningDbContext _db;

    public StudentMasterySummaryQueryAdapter(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<MasterySummary> GetByStudentAsync(int studentId, CancellationToken ct = default)
    {
        // Load mastery rows with enough data to compute per-subject averages.
        // Only rows with at least one attempt (Status != NotStarted) contribute to the average.
        var rows = await _db.StudentSkillMasteries
            .AsNoTracking()
            .Where(m => m.StudentId == studentId && m.Status != MasteryStatus.NotStarted)
            .Select(m => new
            {
                SubjectCode     = m.Skill.Concept.Subject.SubjectCode,
                m.MasteryPercentage,
            })
            .ToListAsync(ct);

        // Per-subject average (integer rounding).
        var bySubjectCode = rows
            .GroupBy(r => r.SubjectCode)
            .ToDictionary(
                g => g.Key,
                g => (int)Math.Round(g.Average(r => r.MasteryPercentage)));

        // Build result: all four subjects always present.
        var bySubject = AllSubjectCodes
            .Select(code => new SubjectMasteryEntry(
                SubjectCode: code,
                Percent:     bySubjectCode.TryGetValue((SubjectCode)code, out var pct) ? pct : 0))
            .ToList()
            .AsReadOnly();

        // Overall: average across all non-NotStarted skill rows; 0 when no rows.
        int overallPercent = rows.Count == 0
            ? 0
            : (int)Math.Round(rows.Average(r => r.MasteryPercentage));

        return new MasterySummary(
            OverallPercent: overallPercent,
            BySubject:      bySubject);
    }
}
