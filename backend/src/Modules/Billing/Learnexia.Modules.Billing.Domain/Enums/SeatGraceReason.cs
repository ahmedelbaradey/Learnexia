namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Reason why a seat grace window was started on the subscription (P10-15-BE-1).
///
/// <para><strong>FINAL MODEL (lead-approved 2026-06-17):</strong> the grace window is triggered
/// ONLY by a payment failure (P10-09 dunning). Voluntary seat removal and plan downgrade are
/// NOT grace events — they are cycle-end effective events, not represented here.</para>
/// </summary>
public enum SeatGraceReason
{
    /// <summary>
    /// Grace was triggered by a payment failure at the renewal boundary (P10-09 charge.failed).
    /// The parent has a 7-day window (config: <c>seats.grace_days</c>) to resolve the charge
    /// before enforcement removes seats from over-limit children.
    /// </summary>
    PaymentFailure = 0,
}
