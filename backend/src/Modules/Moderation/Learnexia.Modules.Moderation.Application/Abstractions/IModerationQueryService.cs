using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Abstractions;

/// <summary>
/// Application-layer seam for the Moderation read side (queue list + detail).
/// The implementation (in Infrastructure) owns the DbContext, EF query composition,
/// AutoMapper ProjectTo, pagination, and UTC-normalization post-pass.
///
/// Callers receive fully-materialised results — no <c>IQueryable</c> or EF types ever cross this boundary.
/// </summary>
public interface IModerationQueryService
{
    /// <summary>
    /// Returns a paged list of moderation queue items, filtered and ordered newest-first (by DetectedAt).
    /// Page inputs are already clamped by the caller (pageNumber ≥ 1, pageSize 1–100).
    /// </summary>
    Task<PaginatedResult<ModerationItemDto>> GetQueuePagedAsync(
        ModerationStatus? status,
        ModerationSource? source,
        string? subjectCode,
        int? grade,
        DateTime? dateFrom,
        DateTime? dateTo,
        string? search,
        int pageNumber,
        int pageSize,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Returns the full detail of a single moderation item by its id.
    /// Returns <c>null</c> when not found.
    /// </summary>
    Task<ModerationItemDetailDto?> GetByIdAsync(
        int id,
        CancellationToken cancellationToken = default);
}
