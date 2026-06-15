using Learnexia.Shared.Kernel.Messaging;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Billing.Application.Features.Refund.Commands.InitiateRefund;

/// <summary>
/// Admin-initiated refund command.
///
/// <para><strong>Authorization:</strong> <c>[Authorize(Policy="Billing.Admin")]</c> is enforced
/// at the controller layer. The handler assumes the caller is already authenticated as admin.</para>
///
/// <para><strong>Flow:</strong> calls <c>IPaymentProvider.Refund</c> (or a no-op on the Fake),
/// records an audit event, and returns. The actual credit-clawback state change is driven by the
/// subsequent <c>refund.succeeded</c> webhook handled by
/// <c>HandleProviderWebhookCommandHandler</c> — not by this command.</para>
/// </summary>
public sealed record InitiateRefundCommand : ICommand<BaseResponse<InitiateRefundResultDto>>
{
    /// <summary>The payment id to refund. Must be &gt; 0 and in <c>Succeeded</c> status.</summary>
    public int PaymentId { get; init; }

    /// <summary>Admin-supplied reason for the refund (audit / logging only).</summary>
    public string Reason { get; init; } = string.Empty;
}

/// <summary>Result DTO returned after initiating a refund.</summary>
public sealed record InitiateRefundResultDto(
    int PaymentId,
    string Message);
