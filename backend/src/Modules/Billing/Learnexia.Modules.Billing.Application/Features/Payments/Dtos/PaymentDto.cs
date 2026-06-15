using Learnexia.Modules.Billing.Domain.Enums;

namespace Learnexia.Modules.Billing.Application.Features.Payments.Dtos;

/// <summary>Read DTO for a single payment record.</summary>
public sealed record PaymentDto
{
    public int Id { get; init; }
    public int? SubscriptionId { get; init; }
    public int ParentUserId { get; init; }
    public string? ProviderPaymentRef { get; init; }
    public decimal Amount { get; init; }
    public string Currency { get; init; } = null!;
    public PaymentStatus Status { get; init; }
    public PaymentKind Kind { get; init; }
    public int? TargetChildId { get; init; }
    public DateTime CreatedAt { get; init; }
}

/// <summary>Result returned from <c>StartSubscriptionCheckoutCommand</c>.</summary>
public sealed record CheckoutResultDto
{
    /// <summary>The internal payment record id.</summary>
    public int PaymentId { get; init; }

    /// <summary>URL the parent's browser should be redirected to complete payment.</summary>
    public string RedirectUrl { get; init; } = null!;

    /// <summary>Opaque provider reference stored on the Payment row.</summary>
    public string ProviderPaymentRef { get; init; } = null!;
}
