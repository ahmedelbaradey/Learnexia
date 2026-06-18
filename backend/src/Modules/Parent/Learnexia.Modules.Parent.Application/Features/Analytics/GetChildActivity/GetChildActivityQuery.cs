using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Parent.Application.Features.Analytics.GetChildActivity;

// E8 — GET /Parent/Children/{id}/Activity
// Read-time composed activity feed from XP time-series, learning stats, and badge seams.
public record GetChildActivityQuery(int ChildId) : IQuery<BaseResponse<ActivityFeedDto>>;
