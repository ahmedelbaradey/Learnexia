using Learnexia.Modules.Billing.Domain.Enums;
using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Represents a single payment attempt within the Billing module.
///
/// <para>Module isolation: <see cref="ParentUserId"/> and <see cref="TargetChildId"/> are plain
/// <c>int</c> values — no cross-module foreign keys. <see cref="SubscriptionId"/> is a
/// Billing-internal FK to <see cref="Subscription"/>.</para>
///
/// <para>Security constraints:
/// <list type="bullet">
///   <item>No card / PAN / CVV data is stored here — web-hosted checkout only.</item>
///   <item><see cref="Amount"/> is always resolved server-side from
///         <see cref="BillingPeriod"/> + <c>IGlobalSettingsProvider</c> keys — never client-supplied.</item>
///   <item><see cref="IdempotencyKey"/> (unique) prevents duplicate payment rows.</item>
/// </list>
/// </para>
/// </summary>
public class Payment : FullAuditedEntity
{
    // ── Ownership ────────────────────────────────────────────────────────────────

    /// <summary>
    /// FK to <see cref="Subscription"/> (Billing-internal). Null for Pack purchases
    /// that are not subscription-linked.
    /// </summary>
    public int? SubscriptionId { get; set; }

    /// <summary>Owning parent user id (plain int — no cross-module FK).</summary>
    public int ParentUserId { get; set; }

    // ── Provider ref ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Opaque reference returned by the payment provider after
    /// <c>CreateCheckoutSession</c>. Used for reconciliation queries.
    /// </summary>
    public string? ProviderPaymentRef { get; set; }

    // ── Money ────────────────────────────────────────────────────────────────────

    /// <summary>Charge amount in the smallest unit of <see cref="Currency"/> (e.g. EGP piastres).</summary>
    public decimal Amount { get; set; }

    /// <summary>Currency code. Always <c>"EGP"</c> for this platform.</summary>
    public string Currency { get; set; } = PaymentCurrency.EGP;

    // ── State ────────────────────────────────────────────────────────────────────

    /// <summary>Lifecycle status of this payment attempt.</summary>
    public PaymentStatus Status { get; set; } = PaymentStatus.Initiated;

    /// <summary>Whether this is a subscription charge or a one-off energy-pack purchase.</summary>
    public PaymentKind Kind { get; set; } = PaymentKind.Subscription;

    // ── Pack purchase ────────────────────────────────────────────────────────────

    /// <summary>
    /// For <see cref="PaymentKind.Pack"/> payments: the child who will receive the energy credits.
    /// Always null for <see cref="PaymentKind.Subscription"/> payments.
    /// </summary>
    public int? TargetChildId { get; set; }

    // ── Idempotency ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Caller-supplied idempotency key (unique DB constraint).
    /// Prevents duplicate payments for the same operation.
    /// </summary>
    public string IdempotencyKey { get; set; } = null!;

}

/// <summary>
/// Currency code constants used by <see cref="Payment"/>.
/// Using a class of constants rather than an enum because currency codes are well-known
/// ISO 4217 strings; an enum would require <c>HasConversion&lt;string&gt;()</c> and add no type safety.
/// </summary>
public static class PaymentCurrency
{
    /// <summary>Egyptian Pound — the only currency in use on this platform.</summary>
    public const string EGP = "EGP";
}
