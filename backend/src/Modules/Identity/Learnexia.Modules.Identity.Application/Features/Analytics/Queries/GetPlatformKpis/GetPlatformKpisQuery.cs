using Learnexia.Modules.Identity.Application.Features.Analytics.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Analytics.Queries.GetPlatformKpis;

/// <summary>
/// Query: return the platform-wide KPI summary over a UTC date range.
/// P7-10 analytics dashboard — <c>GET /api/Admin/Analytics/kpis</c>.
///
/// <para>Implements <c>IQuery&lt;T&gt;</c> — NOT auto-validated (queries bypass <c>ValidationBehavior</c>).
/// All input validation (range, guard checks) runs inside the handler.</para>
///
/// <para>Optional filters (<c>SubjectCode</c>, <c>GradeId</c>, <c>Language</c>) are reserved for
/// future breakdown filtering; the v1 handler assembles the full aggregate from the seams and
/// the breakdown slices are embedded in the DTO (no server-side filter applied in this query —
/// the client picks the slice it wants from <see cref="PlatformKpiSummaryDto.BySubject"/> and
/// <see cref="PlatformKpiSummaryDto.ByGrade"/>).</para>
/// </summary>
public sealed record GetPlatformKpisQuery : IQuery<BaseResponse<PlatformKpiSummaryDto>>
{
    /// <summary>Window start, UTC (inclusive). Defaults to 30 days ago when null.</summary>
    public DateTime? FromUtc { get; init; }

    /// <summary>Window end, UTC (exclusive). Defaults to now (UTC) when null.</summary>
    public DateTime? ToUtc { get; init; }
}
