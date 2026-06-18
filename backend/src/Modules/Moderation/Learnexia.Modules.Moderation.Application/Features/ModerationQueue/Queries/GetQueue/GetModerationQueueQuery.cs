using Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Dtos;
using Learnexia.Modules.Moderation.Domain.Enums;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Pagination;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Moderation.Application.Features.ModerationQueue.Queries.GetQueue;

/// <summary>
/// Paginated, DB-side-filtered moderation queue query (AdminOnly).
/// Queries are NOT auto-validated (ValidationBehavior runs for ICommand only) — input
/// clamping is performed in the handler.
/// </summary>
public record GetModerationQueueQuery : IQuery<BaseResponse<PaginatedResult<ModerationItemDto>>>
{
    /// <summary>Filter by lifecycle status.</summary>
    public ModerationStatus? Status { get; init; }

    /// <summary>Filter by item source (e.g. AiOutput, CurriculumUpload).</summary>
    public ModerationSource? Source { get; init; }

    /// <summary>Filter by subject code (e.g. "Math", "Arabic").</summary>
    public string? SubjectCode { get; init; }

    /// <summary>Filter by school grade (1–12).</summary>
    public int? Grade { get; init; }

    /// <summary>Inclusive lower bound on DetectedAt.</summary>
    public DateTime? DateFrom { get; init; }

    /// <summary>Inclusive upper bound on DetectedAt.</summary>
    public DateTime? DateTo { get; init; }

    /// <summary>Search by ContentReference (partial match).</summary>
    public string? Search { get; init; }

    /// <summary>1-based page number. Defaults to 1.</summary>
    public int PageNumber { get; init; } = 1;

    /// <summary>Page size, bounded to 1–100. Defaults to 20.</summary>
    public int PageSize { get; init; } = 20;
}
