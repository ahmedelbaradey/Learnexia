using Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.FamilyEnergy.Queries.GetFamilyEnergyOverview;

/// <summary>
/// Returns the family energy overview for the authenticated parent (P10-13-BE-10).
///
/// <para>Family-scope IDOR: <see cref="ParentUserId"/> is resolved from the JWT in the handler
/// — never from the request body. A parent can only see their own family wallet.</para>
///
/// <para>Queries do NOT go through <c>ValidationBehavior</c> (CONVENTIONS rule 4).</para>
/// </summary>
public sealed class GetFamilyEnergyOverviewQuery : IQuery<BaseResponse<FamilyEnergyOverviewDto>>
{
    /// <summary>Owning parent's user id — resolved from JWT in the handler, not from the request.</summary>
    public int ParentUserId { get; init; }
}
