using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildReports;

// E6 — GET /Parent/Children/{id}/Reports
// Returns daily XP series (rolling 7-day), 20-day XP trend, and time-of-day buckets.
public record GetChildReportsQuery(int ChildId) : IQuery<BaseResponse<ChildReportsDto>>;
