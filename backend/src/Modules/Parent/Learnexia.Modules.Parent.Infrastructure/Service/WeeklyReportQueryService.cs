using Learnexia.Modules.Parent.Application.Abstractions;
using Learnexia.Modules.Parent.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Parent.Infrastructure.Service;

/// <summary>
/// EF read-only implementation of <see cref="IWeeklyReportQueryService"/>.
///
/// <para>Used exclusively by <c>GetWeeklyReportQueryHandler</c> (Option-C: EF stays in
/// Infrastructure; the Application handler is EF-free).</para>
///
/// <para>All queries use <c>AsNoTracking()</c> — purely read-only; no writes.</para>
/// </summary>
public sealed class WeeklyReportQueryService : IWeeklyReportQueryService
{
    private readonly ParentDbContext _db;

    public WeeklyReportQueryService(ParentDbContext db) => _db = db;

    /// <inheritdoc />
    public async Task<WeeklyReportReadDto?> GetLatestAsync(int childId, CancellationToken ct = default)
    {
        var entity = await _db.WeeklyReports
            .AsNoTracking()
            .Where(r => r.ChildId == childId && r.IsDeleted != true)
            .OrderByDescending(r => r.WeekStartUtc)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToDto(entity);
    }

    /// <inheritdoc />
    public async Task<WeeklyReportReadDto?> GetByWeekAsync(
        int childId,
        DateTime weekStartUtc,
        CancellationToken ct = default)
    {
        var entity = await _db.WeeklyReports
            .AsNoTracking()
            .Where(r => r.ChildId == childId
                     && r.WeekStartUtc == weekStartUtc
                     && r.IsDeleted != true)
            .FirstOrDefaultAsync(ct);

        return entity is null ? null : MapToDto(entity);
    }

    private static WeeklyReportReadDto MapToDto(Domain.Entities.WeeklyReport entity) =>
        new()
        {
            Id                  = entity.Id,
            ChildId             = entity.ChildId,
            WeekStartUtc        = entity.WeekStartUtc,
            XpEarned            = entity.XpEarned,
            SkillsImproved      = entity.SkillsImproved,
            WeakAreasJson       = entity.WeakAreasJson,
            RecommendationsJson = entity.RecommendationsJson,
            GeneratedAtUtc      = entity.GeneratedAtUtc,
        };
}
