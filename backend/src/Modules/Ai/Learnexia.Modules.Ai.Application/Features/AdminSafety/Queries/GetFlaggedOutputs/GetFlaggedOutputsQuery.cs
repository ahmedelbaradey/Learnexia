using Learnexia.Modules.Ai.Application.Features.AdminSafety.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Ai.Application.Features.AdminSafety.Queries.GetFlaggedOutputs;

/// <summary>
/// Paged, filterable list of flagged AI outputs for the admin drill-in (P7-11 AC2).
///
/// Queries are NOT auto-validated; input clamping is performed in the handler.
/// Results are ordered newest-first (by OccurredAtUtc desc).
/// </summary>
public record GetFlaggedOutputsQuery : IQuery<BaseResponse<PaginatedResult<FlaggedOutputDto>>>
{
    /// <summary>Filter by ActionTaken (e.g. "Blocked", "Regenerated", "FallbackReturned").</summary>
    public string? Action { get; init; }

    /// <summary>Filter by a specific reason code stored in the jsonb ReasonCodes array.</summary>
    public string? ReasonCode { get; init; }

    /// <summary>Filter by TaskKind (e.g. "Explain", "Hint", "WhyWrong", "Practice").</summary>
    public string? TaskKind { get; init; }

    /// <summary>Inclusive lower bound on OccurredAtUtc.</summary>
    public DateTime? From { get; init; }

    /// <summary>Inclusive upper bound on OccurredAtUtc.</summary>
    public DateTime? To { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Page size, bounded to 1–100. Defaults to 20.</summary>
    public int PageSize { get; init; } = 20;
}
