using Learnexia.Modules.Identity.Application.Features.Account.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Identity.Application.Features.Account.Queries.GetMyPlan;

/// <summary>
/// Returns the authenticated user's current subscription plan (P2-12 PLAN). Currently a stub returning
/// the Free tier — no DB hit. Queries are NOT auto-validated (CONVENTIONS §4).
/// </summary>
/// <remarks>
/// TODO P2-12-PAYMENTS: wire up a real plan lookup once the payments module exists.
/// </remarks>
public sealed record GetMyPlanQuery : IQuery<BaseResponse<CurrentPlanResponse>>;
