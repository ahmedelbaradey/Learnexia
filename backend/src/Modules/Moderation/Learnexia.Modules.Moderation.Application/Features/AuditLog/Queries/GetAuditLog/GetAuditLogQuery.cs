using Learnexia.Modules.Moderation.Application.Features.AuditLog.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.AuditLog.Queries.GetAuditLog;

/// <summary>
/// Paginated, DB-side-filtered audit log query (AdminOnly).
/// Queries are NOT auto-validated (ValidationBehavior runs for ICommand only) — input
/// validation is performed in the handler.
/// </summary>
public record GetAuditLogQuery : IQuery<BaseResponse<PaginatedResult<AuditLogDto>>>
{
    /// <summary>Filter by acting admin user ID.</summary>
    public int? AdminUserId { get; init; }

    /// <summary>Filter by action type string (e.g. "Subject.Created").</summary>
    public string? ActionType { get; init; }

    /// <summary>Filter by target entity type (e.g. "Subject", "Unit").</summary>
    public string? TargetEntityType { get; init; }

    /// <summary>Inclusive lower bound on OccurredAtUtc.</summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>Inclusive upper bound on OccurredAtUtc.</summary>
    public DateTime? DateTo { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Page size, bounded to 1–100. Defaults to 20.</summary>
    public int PageSize { get; init; } = 20;
}
