namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Raised (post-commit) by <c>HandleProviderWebhookCommandHandler</c> when a
/// <c>payment.succeeded</c> webhook is processed and the parent's subscription is
/// flipped to <c>Active Premium</c>.
///
/// <para>Consumers:</para>
/// <list type="bullet">
///   <item><c>BillingGrantJob</c> (if triggered on activation) — top-up the family's
///         children to the Premium monthly allowance.</item>
///   <item>Notifications module — send "Premium activated" push/email to the parent.</item>
/// </list>
///
/// <para>Module isolation: opaque int IDs only — no PII, no cross-module FK.</para>
/// </summary>
public sealed record SubscriptionActivatedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int ParentUserId,
    int SubscriptionId,
    int PaymentId,
    string BillingPeriod) : IIntegrationEvent;
