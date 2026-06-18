namespace Learnexia.Modules.Billing.Domain.Enums;

/// <summary>
/// Machine-readable reason for a ledger entry. Stable — do not rename existing members.
/// </summary>
public enum CreditReasonCode
{
    /// <summary>Unspecified / legacy.</summary>
    Unspecified = 0,

    /// <summary>Monthly energy grant (free plan).</summary>
    MonthlyGrantFree = 1,

    /// <summary>Monthly energy grant (premium plan).</summary>
    MonthlyGrantPremium = 2,

    /// <summary>AI Hint response delivered.</summary>
    AiHint = 10,

    /// <summary>AI WhyWrong (explain mistake) response delivered.</summary>
    AiWhyWrong = 11,

    /// <summary>AI deep explanation response delivered.</summary>
    AiDeepExplanation = 12,

    /// <summary>AI practice generation response delivered.</summary>
    AiPracticeGeneration = 13,

    /// <summary>Expiry of unused granted balance at cycle end.</summary>
    GrantExpiry = 20,

    /// <summary>One-off energy pack purchase.</summary>
    PackPurchase = 30,

    /// <summary>Refund of unspent purchased credits.</summary>
    PackRefund = 40,

    /// <summary>Admin manual positive adjustment.</summary>
    AdminCredit = 50,

    /// <summary>Admin manual negative adjustment.</summary>
    AdminDebit = 51,

    // ── P10-13 family-wallet reason codes ────────────────────────────────────────

    /// <summary>Monthly subscription grant allocated to a child's <c>ChildEnergyAllocation</c> row.</summary>
    GrantAllocation = 60,

    /// <summary>AI spend debited from the child's own <c>ChildEnergyAllocation</c> (allocation-first path).</summary>
    SpendAllocation = 61,

    /// <summary>AI spend shortfall drawn from the shared family <c>PurchasedBalance</c> (fallback path).</summary>
    SpendPurchasedFallback = 62,

    /// <summary>Subscription balance + per-child allocations expired at cycle rollover. Purchased untouched.</summary>
    SubscriptionExpire = 63,

    // ── P10-16 redistribution reason codes ───────────────────────────────────────

    /// <summary>Intra-family allocation transfer in (destination child receives amount). Paired with <c>TransferOut</c> via <c>CorrelationId</c>.</summary>
    TransferIn = 70,

    /// <summary>Intra-family allocation transfer out (source child loses amount). Paired with <c>TransferIn</c> via <c>CorrelationId</c>.</summary>
    TransferOut = 71,

    // ── P10-15 seat-lock reason codes ─────────────────────────────────────────────

    /// <summary>Child seat locked (enforcement; no active seat). No balance movement.</summary>
    SeatLock = 80,

    /// <summary>Unspent allocation forfeited when a child is seat-locked during enforcement.</summary>
    SeatLockForfeit = 81,

    // ── P10-17 purchased-energy refund ────────────────────────────────────────────

    /// <summary>Refund of unused purchased energy (bucket B only). Settlement via verified refund webhook.</summary>
    PurchasedRefund = 90,

    // ── P3-14 Lexi recommendation narration ──────────────────────────────────────

    /// <summary>AI Lexi recommendation narration response delivered (P3-14). Cost = 5 (Practice tier).</summary>
    AiRecommendation = 14,
}
