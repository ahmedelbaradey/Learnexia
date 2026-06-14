using Learnexia.Modules.Learning.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Learning;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Learning.Infrastructure.Contracts;

/// <summary>
/// Implements <see cref="ILessonContextContract"/> by querying <see cref="LearningDbContext"/>
/// for minimal lesson metadata (title, subject, grade) required by the AI helper (P3-04).
///
/// <para>Cross-module isolation: the Ai module injects <see cref="ILessonContextContract"/>
/// from <c>Shared.Contracts</c> — it never references this adapter or any Learning project
/// directly. This adapter lives in <c>Learning.Infrastructure</c> and is registered in
/// <c>AddLearningInfrastructure</c>.</para>
///
/// <para>The query joins Lesson → Unit → Subject to resolve SubjectId and GradeId.
/// Soft-deleted lessons are excluded by the global query filter on <see cref="LearningDbContext"/>.</para>
/// </summary>
internal sealed class LessonContextContractAdapter : ILessonContextContract
{
    private readonly LearningDbContext _db;

    public LessonContextContractAdapter(LearningDbContext db)
    {
        _db = db;
    }

    /// <inheritdoc />
    public async Task<LessonContextDto?> GetLessonContextAsync(int lessonId, CancellationToken ct = default)
    {
        return await _db.Lessons
            .AsNoTracking()
            .Where(l => l.Id == lessonId)
            .Select(l => new LessonContextDto(
                l.Id,
                l.Name,
                l.Unit.SubjectId,
                l.Unit.Subject.GradeId))
            .FirstOrDefaultAsync(ct);
    }
}
