using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Queries.ReconcileAccount;

/// <summary>
/// Admin/ops query: recomputes both pool balances from the append-only ledger and compares
/// against the stored balances on <c>CreditAccount</c>. Reports drift but does NOT auto-heal.
/// No <c>ValidationBehavior</c> runs on queries (rule 4).
/// </summary>
public record ReconcileAccountQuery : IQuery<BaseResponse<ReconciliationResultDto>>
{
    public int ChildId { get; init; }
}
