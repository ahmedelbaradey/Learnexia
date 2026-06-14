using System.Collections.Concurrent;

namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Minimal per-student fixed-window rate limiter for the AI tutor explain endpoint (P3-04 BE-5).
///
/// <para>Guards against cost/abuse by bounding the number of runtime AI calls per student per
/// time window. Uses a <see cref="ConcurrentDictionary"/> as the counter store — in-process only.
/// Registered as Singleton so the counter persists across requests within the same process.</para>
///
/// <para><strong>Reuse note:</strong> the existing AspNetCoreRateLimit infrastructure (P1-13b)
/// is IP-based and does not support per-authenticated-user keying for this student-scoped use
/// case. This minimal in-process guard is intentional for the MVP slice (P3-04). A production-
/// grade per-student distributed rate limit (Redis SETNX) can replace this later without
/// changing callers.</para>
///
/// <para>Current defaults: <strong>10 requests per 60 seconds per student</strong>
/// (cost/abuse bound for the explain endpoint).</para>
/// </summary>
public sealed class AiTutorRateLimiter
{
    // Fixed-window defaults — intentionally conservative for MVP.
    private const int MaxRequestsPerWindow = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly record struct WindowCounter(int Count, DateTime WindowStart);

    private readonly ConcurrentDictionary<int, WindowCounter> _counters = new();

    /// <summary>
    /// Returns <c>true</c> when the student is within the allowed rate for the explain endpoint;
    /// <c>false</c> when the window limit has been exceeded.
    /// </summary>
    public bool TryAllow(int studentId)
    {
        var now = DateTime.UtcNow;

        var updated = _counters.AddOrUpdate(
            studentId,
            // First request — create a new window.
            _ => new WindowCounter(1, now),
            (_, existing) =>
            {
                // If the window has expired, reset.
                if (now - existing.WindowStart >= Window)
                    return new WindowCounter(1, now);

                // Window still active — increment (may exceed the max, check below).
                return existing with { Count = existing.Count + 1 };
            });

        return updated.Count <= MaxRequestsPerWindow;
    }
}
