namespace Learnexia.Shared.Contracts.Billing;

/// <summary>
/// Raised (post-commit) by <c>HandleProviderWebhookCommandHandler</c> when a
/// <c>payment.failed</c> webhook is processed and the <c>Payment</c> row is flipped
/// to <c>Failed</c>.
///
/// <para>Consumers:</para>
/// <list type="bullet">
///   <item>Notifications module (P10-09) — dunning notifications to the parent.</item>
///   <item><c>DunningRetryJob</c> (P10-09) — schedules a retry.</item>
/// </list>
///
/// <para>Module isolation: opaque int IDs only — no PII, no card data.</para>
/// </summary>
public sealed record PaymentFailedIntegrationEvent(
    Guid EventId,
    DateTime OccurredOnUtc,
    int ParentUserId,
    int PaymentId,
    int? SubscriptionId) : IIntegrationEvent;
