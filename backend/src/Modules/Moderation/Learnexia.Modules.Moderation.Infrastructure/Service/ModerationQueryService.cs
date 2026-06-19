using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Infrastructure.Service;

/// <summary>
/// Option-C implementation of <see cref="IModerationQueryService"/>.
/// All EF Core concerns (IQueryable, filters, ProjectTo, pagination, UTC normalization)
/// are composed here — inside the Infrastructure layer that owns the DbContext —
/// so the Application layer and query handlers stay completely EF-free.
///
/// <para>Filtering, ordering, and <c>ProjectTo</c> are applied before materialisation so
/// the generated SQL carries all predicates and the LIMIT/OFFSET; no client-side evaluation.</para>
///
/// <para>UTC normalization: Npgsql.EnableLegacyTimestampBehavior=true causes a <c>timestamptz</c>
/// column to materialise as <see cref="DateTimeKind.Local"/> on the reading side. The
/// <c>ToUniversalTime()</c> call at the post-projection boundary ensures timestamps are
/// always emitted as true UTC instants (…Z suffix) regardless of the server's local timezone.</para>
/// </summary>
internal sealed class ModerationQueryService : IModerationQueryService
{
    private readonly ModerationDbContext _db;
    private readonly IMapper _mapper;

    public ModerationQueryService(ModerationDbContext db, IMapper mapper)
    {
        _db     = db;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<ModerationItemDto>> GetQueuePagedAsync(
        ModerationStatus? status,
        ModerationSource? source,
        string? subjectCode,
        int? grade,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Build the filtered IQueryable entirely DB-side — all predicates applied before
        // ProjectTo so EF can translate them into a single SQL WHERE clause.
        var query = _db.ModerationItems.AsQueryable();

        if (status.HasValue)
            query = query.Where(m => m.Status == status.Value);

        if (source.HasValue)
            query = query.Where(m => m.Source == source.Value);

        if (!string.IsNullOrWhiteSpace(subjectCode))
            query = query.Where(m => m.SubjectCode == subjectCode);

        if (grade.HasValue)
            query = query.Where(m => m.Grade == grade.Value);

        if (dateFrom.HasValue)
            query = query.Where(m => m.DetectedAt >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(m => m.DetectedAt <= dateTo.Value);

        if (!string.IsNullOrWhiteSpace(search))
            query = query.Where(m => m.ContentReference.Contains(search));

        // Default ordering: newest detection first.
        query = query.OrderByDescending(m => m.DetectedAt);

        var result = await _mapper.ProjectTo<ModerationItemDto>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy: null);

        // UTC normalization (P4-11): Npgsql legacy-timestamp reads timestamptz as Kind=Local;
        // normalize at the service boundary so the handler and JSON serializer always see UTC.
        if (result.Data is { Count: > 0 })
            result.Data = result.Data
                .Select(d => d with
                {
                    DetectedAt = d.DetectedAt.ToUniversalTime(),
                    CreatedAt  = d.CreatedAt.ToUniversalTime(),
                })
                .ToList();

        return result;
    }

    public async Task<ModerationItemDetailDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default)
    {
        var dto = await _mapper.ProjectTo<ModerationItemDetailDto>(
                _db.ModerationItems.Where(m => m.Id == id))
            .FirstOrDefaultAsync(cancellationToken);

        if (dto is null)
            return null;

        // UTC normalization.
        return dto with
        {
            DetectedAt    = dto.DetectedAt.ToUniversalTime(),
            CreatedAt     = dto.CreatedAt.ToUniversalTime(),
            ReviewedAtUtc = dto.ReviewedAtUtc.HasValue
                ? dto.ReviewedAtUtc.Value.ToUniversalTime()
                : null,
        };
    }
}
