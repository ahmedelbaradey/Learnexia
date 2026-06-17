using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;

/// <summary>
/// Admin/ops query: recomputes the family wallet balances from the append-only ledger
/// and compares against the stored balances on <c>FamilyEnergyAccount</c>.
/// Family-scoped (by <see cref="ParentId"/>). Reports drift but does NOT auto-heal.
/// No <c>ValidationBehavior</c> runs on queries (rule 4).
/// Does NOT touch <c>CreditAccount</c>.
/// </summary>
public record ReconcileAccountQuery : IQuery<BaseResponse<ReconciliationResultDto>>
{
    /// <summary>Parent user ID whose family wallet is reconciled.</summary>
    public int ParentId { get; init; }
}
