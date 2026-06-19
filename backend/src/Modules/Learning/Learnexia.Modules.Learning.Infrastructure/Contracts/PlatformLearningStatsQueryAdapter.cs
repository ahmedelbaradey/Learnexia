using Learnexia.Modules.Learning.Domain.Enums;
using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="IPlatformLearningStatsQuery"/> by querying <see cref="LearningDbContext"/>
/// for windowed platform-wide learning statistics required by the P7-10 analytics dashboard.
///
/// <para>Cross-module isolation: the Identity façade injects <see cref="IPlatformLearningStatsQuery"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Learning project
/// directly. This adapter lives in <c>Learning.Infrastructure</c> and is registered in
/// <c>AddLearningInfrastructure</c>.</para>
///
/// <para>Subject/grade breakdown: the <c>Attempt</c> entity holds <c>LessonId</c> only (plain int, no FK).
/// To resolve subject code, language, and grade we join through Lesson→Unit→Subject→Grade via
/// EF navigation properties — all within the Learning schema (no cross-module join).</para>
///
/// <para>OQ-2 (brief): there is only ONE attempt type in the Learning module — the <c>Attempt</c> entity
/// represents a student's quiz session for a lesson's question set. "Quizzes completed" is therefore the
/// same count as "lessons completed with a completed attempt". No separate quiz-vs-lesson distinction exists
/// at the row level; the façade surfaces one <c>LessonsCompleted</c> counter accordingly.</para>
///
/// <para>All results are sentinel-safe: an empty window returns zeroed/empty stats — never null, never throws.</para>
/// </summary>
public sealed class PlatformLearningStatsQueryAdapter : IPlatformLearningStatsQuery
{
    private readonly LearningDbContext _db;

    public PlatformLearningStatsQueryAdapter(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc/>
    public async Task<PlatformLearningStats> GetPlatformAsync(
        DateTime fromUtc,
        DateTime toUtc,
        CancellationToken ct = default)
    {
        // ── Aggregate top-level KPIs ──────────────────────────────────────────────────────────────
        // Group-by over completed Attempts in the window. IgnoreQueryFilters not needed here —
        // Attempt has no global soft-delete filter (see LearningDbContext.OnModelCreating).

        var attempts = await _db.Attempts
            .AsNoTracking()
            .Where(a => a.Status == AttemptStatus.Completed
                     && a.CompletedAt.HasValue
                     && a.CompletedAt.Value >= fromUtc
                     && a.CompletedAt.Value < toUtc)
            .Select(a => new { a.LessonId, a.StudentId })
            .ToListAsync(ct);

        if (attempts.Count == 0)
        {
            return new PlatformLearningStats(
                LessonsCompleted: 0,
                TotalAttempts: 0,
                DistinctActiveStudents: 0,
                BySubject: [],
                ByGrade: [],
                ByLanguage: []);
        }

        int lessonsCompleted = attempts.Select(a => a.LessonId).Distinct().Count();
        int totalAttempts = attempts.Count;
        int distinctActiveStudents = attempts.Select(a => a.StudentId).Distinct().Count();

        // ── Subject + grade breakdowns ────────────────────────────────────────────────────────────
        // Join Lesson → Unit → Subject → Grade entirely within the Learning schema.
        // IgnoreQueryFilters() used here so soft-deleted subjects/units/lessons don't cause rows to vanish
        // retroactively from completed attempts (the attempt is already persisted; the lesson may have been
        // soft-deleted later). We only want to resolve the subject/grade of the lesson referenced by the attempt.

        var lessonsInWindow = attempts.Select(a => a.LessonId).Distinct().ToList();

        var lessonContexts = await _db.Lessons
            .AsNoTracking()
            .IgnoreQueryFilters()
            .Where(l => lessonsInWindow.Contains(l.Id))
            .Select(l => new
            {
                l.Id,
                SubjectCode = (int)l.Unit.Subject.SubjectCode,
                Language    = (int)l.Unit.Subject.Language,
                GradeId     = l.Unit.Subject.GradeId,
            })
            .ToListAsync(ct);

        // Build a lookup: lessonId → (SubjectCode, Language, GradeId)
        var lessonMeta = lessonContexts.ToDictionary(lc => lc.Id);

        // Resolve per-attempt enriched rows
        var enriched = attempts
            .Select(a => lessonMeta.TryGetValue(a.LessonId, out var meta)
                ? (LessonId: a.LessonId, StudentId: a.StudentId,
                   SubjectCode: meta.SubjectCode, Language: meta.Language, GradeId: meta.GradeId)
                : (LessonId: a.LessonId, StudentId: a.StudentId,
                   SubjectCode: -1, Language: -1, GradeId: -1))    // -1 = unresolved
            .Where(e => e.SubjectCode >= 0)                         // drop unresolved
            .ToList();

        // BySubject group-by
        var bySubject = enriched
            .GroupBy(e => new { e.SubjectCode, e.Language })
            .Select(g => new SubjectBreakdown(
                SubjectCode:           g.Key.SubjectCode,
                Language:              g.Key.Language,
                LessonsCompleted:      g.Select(e => e.LessonId).Distinct().Count(),
                TotalAttempts:         g.Count(),
                DistinctActiveStudents: g.Select(e => e.StudentId).Distinct().Count()))
            .OrderBy(b => b.SubjectCode).ThenBy(b => b.Language)
            .ToList();

        // ByGrade group-by
        var byGrade = enriched
            .GroupBy(e => e.GradeId)
            .Select(g => new GradeBreakdown(
                GradeId:               g.Key,
                LessonsCompleted:      g.Select(e => e.LessonId).Distinct().Count(),
                TotalAttempts:         g.Count(),
                DistinctActiveStudents: g.Select(e => e.StudentId).Distinct().Count()))
            .OrderBy(b => b.GradeId)
            .ToList();

        // ByLanguage group-by — curriculum is bilingual parallel trees, so language (ar/en)
        // is a first-class breakdown dimension alongside subject and grade (P7-10 AC).
        var byLanguage = enriched
            .GroupBy(e => e.Language)
            .Select(g => new LanguageBreakdown(
                Language:              g.Key,
                LessonsCompleted:      g.Select(e => e.LessonId).Distinct().Count(),
                TotalAttempts:         g.Count(),
                DistinctActiveStudents: g.Select(e => e.StudentId).Distinct().Count()))
            .OrderBy(b => b.Language)
            .ToList();

        return new PlatformLearningStats(
            LessonsCompleted:       lessonsCompleted,
            TotalAttempts:          totalAttempts,
            DistinctActiveStudents: distinctActiveStudents,
            BySubject:              bySubject,
            ByGrade:                byGrade,
            ByLanguage:             byLanguage);
    }
}
