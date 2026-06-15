using Learnexia.Modules.Billing.Application.Features.EnergyStatus.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.EnergyStatus.Queries.GetEnergyStatus;

/// <summary>
/// Returns the child's current energy status: balance (granted + purchased), daily usage vs cap,
/// grant expiry, and a typed <c>WarningState</c>.
///
/// <para>Child-scoped IDOR-safe read seam for the P10-10 kid energy UI (FE) and P10-03
/// pre-authorize check. Authentication enforced at the controller layer.</para>
///
/// <para>Queries do NOT go through <c>ValidationBehavior</c> (rule 4) — validation is done
/// in the handler.</para>
/// </summary>
public sealed class EnergyStatusQuery : IQuery<BaseResponse<EnergyStatusDto>>
{
    /// <summary>The child whose energy status to retrieve (plain int — no cross-module FK).</summary>
    public int ChildId { get; init; }
}
