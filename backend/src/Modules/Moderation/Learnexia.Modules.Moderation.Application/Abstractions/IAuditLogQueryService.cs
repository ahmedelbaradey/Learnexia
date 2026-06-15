using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer seam for the Moderation read side.
/// The implementation (in Infrastructure) owns the DbContext, EF query composition,
/// AutoMapper ProjectTo, pagination, and the UTC-normalization post-pass.
///
/// Callers receive a fully-materialised <see cref="PaginatedResult{T}"/> — no
/// <c>IQueryable</c> or EF types ever cross this boundary.
/// </summary>
public interface IAuditLogQueryService
{
    /// <summary>
    /// Returns a page of audit-log entries, filtered and ordered newest-first.
    /// Page inputs are already clamped by the caller (pageNumber ≥ 1, pageSize 1–100).
    /// <c>OccurredAtUtc</c> values in the returned DTOs are normalised to UTC Kind.
    /// </summary>
    Task<PaginatedResult<AuditLogDto>> GetPagedAsync(
        int? adminUserId,
        string? actionType,
        string? targetEntityType,
        DateTime? dateFrom,
        DateTime? dateTo,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);
}
