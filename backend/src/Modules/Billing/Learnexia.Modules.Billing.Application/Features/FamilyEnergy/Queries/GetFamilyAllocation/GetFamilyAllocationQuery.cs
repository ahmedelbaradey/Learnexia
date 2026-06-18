using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyAllocation;

/// <summary>
/// Query: return per-child allocation split for ALL active-seat children of the authenticated parent
/// (P10-16-BE-4).
///
/// <para><strong>IDOR guard:</strong> <c>ParentUserId</c> is resolved from the JWT inside the handler
/// via <c>ICurrentUserService.UserId</c> — never supplied by the client.</para>
///
/// <para><strong>Mid-cycle children:</strong> active-seat children with no current-cycle
/// <c>ChildEnergyAllocation</c> row are included with <c>Allocated=0, Spent=0, Remaining=0,
/// IsMidCycleNoGrant=true</c>.</para>
///
/// <para>Queries do NOT go through <c>ValidationBehavior</c> (CONVENTIONS rule 4).</para>
/// </summary>
public sealed record GetFamilyAllocationQuery : IQuery<BaseResponse<FamilyAllocationDto>>;
