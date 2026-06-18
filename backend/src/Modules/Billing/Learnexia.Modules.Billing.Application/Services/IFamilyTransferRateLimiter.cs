namespace Learnexia.Modules.Billing.Application.Services;

/// <summary>
/// Per-parent fixed-window rate limiter for the family energy <c>Transfer</c> endpoint (P10-16 hardening).
///
/// <para>Defence-in-depth against accidental client retry-loops and abuse on the redistribution
/// endpoint. Mirrors <c>IAiTutorRateLimiter</c> in the Ai module — in-process counter, registered
/// Singleton. A distributed (Redis) limiter can replace the implementation later without changing
/// callers.</para>
/// </summary>
public interface IFamilyTransferRateLimiter
{
    /// <summary>
    /// Returns <c>true</c> when the parent is within the allowed transfer rate;
    /// <c>false</c> when the window limit has been exceeded.
    /// </summary>
    bool TryAllow(int parentUserId);
}
