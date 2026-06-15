using AutoMapper;
using AutoMapper.QueryableExtensions;
using Learnexia.Modules.Moderation.Application.Abstractions;
using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Modules.Moderation.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Moderation.Infrastructure.Service;

/// <summary>
/// Option-C implementation of <see cref="IAuditLogQueryService"/>.
/// All EF Core concerns (IQueryable, filters, ProjectTo, pagination, UTC normalization)
/// are composed here — inside the Infrastructure layer that owns the DbContext —
/// so the Application layer and the query handler stay completely EF-free.
///
/// <para>Filtering, ordering, and <c>ProjectTo</c> are applied before materialisation so
/// the generated SQL carries all predicates and the LIMIT/OFFSET; no client-side evaluation.</para>
///
/// <para>UTC normalization: Npgsql.EnableLegacyTimestampBehavior=true causes a <c>timestamptz</c>
/// column to materialise as <see cref="DateTimeKind.Local"/> on the reading side. The
/// <c>ToUniversalTime()</c> call at the post-projection boundary (P4-11 convention) ensures
/// <c>OccurredAtUtc</c> is always emitted as a true UTC instant (…Z suffix) regardless of
/// the server's local timezone.</para>
/// </summary>
internal sealed class AuditLogQueryService : IAuditLogQueryService
{
    private readonly ModerationDbContext _db;
    private readonly IMapper _mapper;

    public AuditLogQueryService(ModerationDbContext db, IMapper mapper)
    {
        _db = db;
        _mapper = mapper;
    }

    public async Task<PaginatedResult<AuditLogDto>> GetPagedAsync(
        int? adminUserId,
        string? actionType,
        string? targetEntityType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default)
    {
        // Build the filtered IQueryable entirely DB-side — all predicates applied before
        // ProjectTo so EF can translate them into a single SQL WHERE clause.
        var query = _db.AuditLogs.AsQueryable();

        if (adminUserId.HasValue)
            query = query.Where(a => a.AdminUserId == adminUserId.Value);

        if (!string.IsNullOrWhiteSpace(actionType))
            query = query.Where(a => a.Action == actionType);

        if (!string.IsNullOrWhiteSpace(targetEntityType))
            query = query.Where(a => a.TargetEntityType == targetEntityType);

        if (dateFrom.HasValue)
            query = query.Where(a => a.OccurredAtUtc >= dateFrom.Value);

        if (dateTo.HasValue)
            query = query.Where(a => a.OccurredAtUtc <= dateTo.Value);

        // Default ordering: newest action first.
        query = query.OrderByDescending(a => a.OccurredAtUtc);

        var result = await _mapper.ProjectTo<AuditLogDto>(query)
            .ToPaginatedListAsync(pageNumber, pageSize, orderBy: null);

        // UTC normalization (P4-11): Npgsql legacy-timestamp reads timestamptz as Kind=Local;
        // normalize at the service boundary so the handler and JSON serializer always see UTC.
        if (result.Data is { Count: > 0 })
            result.Data = result.Data
                .Select(d => d with { OccurredAtUtc = d.OccurredAtUtc.ToUniversalTime() })
                .ToList();

        return result;
    }
}
