namespace Learnexia.Modules.Billing.Application.Features.Payments.Dtos;

/// <summary>Response DTO returned from <c>StartPackCheckoutCommand</c>.</summary>
public sealed record PackCheckoutResultDto
{
    /// <summary>The internal Payment row id for this pack purchase attempt.</summary>
    public int PaymentId { get; init; }

    /// <summary>URL the parent's browser should be redirected to in order to complete payment.</summary>
    public string RedirectUrl { get; init; } = null!;

    /// <summary>Opaque provider reference stored on the Payment row (for reconciliation).</summary>
    public string ProviderPaymentRef { get; init; } = null!;
}
