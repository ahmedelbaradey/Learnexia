using System.Collections.Concurrent;

namespace Learnexia.Modules.Billing.Application.Services;

/// <summary>
/// Minimal per-parent fixed-window rate limiter for the family energy <c>Transfer</c> endpoint
/// (P10-16 hardening). Mirrors <c>AiTutorRateLimiter</c> in the Ai module.
///
/// <para>Guards against accidental retry-loops / abuse by bounding the number of transfer calls
/// per parent per window. Uses a <see cref="ConcurrentDictionary"/> as the counter store —
/// in-process only. Registered as Singleton so the counter persists across requests within the
/// same process.</para>
///
/// <para>Current defaults: <strong>10 requests per 60 seconds per parent</strong> — a parent moving
/// energy between their own children is a low-frequency operation; this comfortably allows genuine
/// rebalancing while stopping runaway loops. A production-grade distributed limiter (Redis) can
/// replace this later without changing callers.</para>
/// </summary>
public sealed class FamilyTransferRateLimiter : IFamilyTransferRateLimiter
{
    // Fixed-window defaults — intentionally conservative.
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly record struct WindowCounter(int Count, DateTime WindowStart);

    private readonly ConcurrentDictionary<int, WindowCounter> _counters = new();

    /// <inheritdoc />
    public bool TryAllow(int parentUserId)
    {
        var now = DateTime.UtcNow;

        var updated = _counters.AddOrUpdate(
            parentUserId,
            // First request — create a new window.
            _ => new WindowCounter(1, now),
            (_, existing) =>
            {
                // If the window has expired, reset.
                if (now - existing.WindowStart >= Window)
                    return new WindowCounter(1, now);

                // Window still active — increment (may exceed the max, checked below).
                return existing with { Count = existing.Count + 1 };
            });

        return updated.Count <= MaxRequestsPerWindow;
    }
}
