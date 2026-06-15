using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Billing.Domain.Entities;

/// <summary>
/// Idempotency store for incoming payment provider webhooks.
///
/// <para>The unique index on <see cref="ProviderEventId"/> is the sole guard against
/// double-processing a replayed or duplicated webhook. Before any state mutation the
/// handler inserts a <see cref="WebhookEvent"/> row; a unique-violation means the event
/// was already processed → no-op return.</para>
///
/// <para>Payload is stored as raw JSON for audit / replay purposes. It MUST NOT be
/// reflected back in any API response (could leak provider details / amounts).</para>
/// </summary>
public class WebhookEvent : FullAuditedEntity
{
    /// <summary>
    /// Provider-assigned unique event id (e.g. Paymob transaction id, Fake guid).
    /// DB-unique: duplicate = already processed.
    /// </summary>
    public string ProviderEventId { get; set; } = null!;

    /// <summary>Provider event type string (e.g. <c>"payment.succeeded"</c>).</summary>
    public string EventType { get; set; } = null!;

    /// <summary>Raw JSON payload from the provider. Stored for audit; never returned to clients.</summary>
    public string Payload { get; set; } = null!;

    /// <summary>UTC timestamp when this event was fully processed (all state mutations committed).</summary>
    public DateTime? ProcessedAt { get; set; }

    /// <summary>
    /// Whether the event was processed successfully (<c>true</c>) or was received but could
    /// not be fully processed (<c>false</c>) — e.g. no matching <c>Payment</c> row found.
    /// </summary>
    public bool Succeeded { get; set; }
}
