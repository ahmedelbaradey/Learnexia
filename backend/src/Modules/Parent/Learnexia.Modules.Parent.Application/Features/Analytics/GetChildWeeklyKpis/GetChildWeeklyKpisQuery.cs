using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildWeeklyKpis;

// E3 — GET /Parent/Children/{id}/WeeklyKpis
// Rolling-7-day window + prior-window WoW deltas.
public record GetChildWeeklyKpisQuery(int ChildId) : IQuery<BaseResponse<WeeklyKpisDto>>;
