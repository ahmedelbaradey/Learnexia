using Learnexia.Modules.Billing.Application.Features.Credits.Dtos;
using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Credits.Commands.GrantCredit;

/// <summary>
/// Admin comp: deposits energy into the shared family <see cref="Domain.Entities.FamilyEnergyAccount.PurchasedBalance"/>
/// (permanent admin compensation) and writes an immutable ledger row.
///
/// <para>This grant is family-scoped: it targets the family wallet for <see cref="ParentId"/>,
/// NOT a per-child <c>CreditAccount</c> (which is retired). No per-child account is touched.
/// Idempotent: same <see cref="IdempotencyKey"/> → no-op.</para>
/// </summary>
public record GrantCreditCommand : ICommand<BaseResponse<DebitResultDto>>
{
    /// <summary>Parent user ID whose family wallet receives the admin comp grant.</summary>
    public int ParentId { get; init; }

    /// <summary>Amount of energy to deposit into the shared <c>PurchasedBalance</c> (admin comp).</summary>
    public int Amount { get; init; }

    /// <summary>Idempotency key for the grant operation (typically <c>admin-grant:{parentId}:{yyyyMM}</c>).</summary>
    public string IdempotencyKey { get; init; } = null!;
}
