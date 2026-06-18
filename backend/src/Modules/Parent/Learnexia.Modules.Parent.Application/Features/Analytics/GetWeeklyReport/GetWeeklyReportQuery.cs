using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetWeeklyReport;

/// <summary>
/// E9 — GET /Parent/Children/{id}/WeeklyReport?week=yyyy-MM-dd
///
/// <para>
/// <paramref name="WeekStartUtc"/> is optional. When null the handler returns the latest
/// stored report. When provided it must be the Monday of the requested ISO week.
/// </para>
/// </summary>
// Queries are NOT auto-validated (rule 4) — the handler validates inline.
public record GetWeeklyReportQuery(int ChildId, DateTime? WeekStartUtc = null)
    : IQuery<BaseResponse<WeeklyReportDto>>;
