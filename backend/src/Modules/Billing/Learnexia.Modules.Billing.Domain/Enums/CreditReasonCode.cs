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
}
