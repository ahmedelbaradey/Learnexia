namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Lifecycle status of a <see cref="Entities.Payment"/> record.
///
/// <para>Terminal states: <see cref="Succeeded"/>, <see cref="Failed"/>, <see cref="Refunded"/>.</para>
/// </summary>
public enum PaymentStatus
{
    /// <summary>
    /// Created locally; checkout session dispatched to the provider.
    /// Not yet confirmed by the provider.
    /// </summary>
    Initiated = 0,

    /// <summary>
    /// Provider acknowledged the charge is processing (pre-authorization).
    /// Intermediate state used by some providers — not required by the Fake.
    /// </summary>
    Pending = 1,

    /// <summary>Payment collected successfully. Terminal.</summary>
    Succeeded = 2,

    /// <summary>Payment declined or failed. Terminal.</summary>
    Failed = 3,

    /// <summary>Payment was refunded. Terminal.</summary>
    Refunded = 4,
}
