namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Per-student rate limiter seam for the AI tutor endpoints.
///
/// <para>The MVP implementation (<c>AiTutorRateLimiter</c>) uses an in-process
/// <c>ConcurrentDictionary</c> and is registered when Redis is absent.
/// The production implementation (<c>RedisAiRateLimiter</c> in <c>Ai.Infrastructure</c>)
/// stores the counter in Redis so state is shared across multiple app instances.</para>
///
/// <para>Both implementations register as the <c>IAiTutorRateLimiter</c> singleton.
/// All 4 AI handler constructors inject the interface — no concrete dependency.</para>
/// </summary>
public interface IAiTutorRateLimiter
{
    /// <summary>
    /// Returns <c>true</c> when the student is within the allowed rate for the current window;
    /// <c>false</c> when the window limit has been exceeded.
    ///
    /// <para>Implementations MUST be fail-soft: any Redis error → degrade to allow (return
    /// <c>true</c>) so a Redis outage never hard-blocks a student from getting help.</para>
    /// </summary>
    bool TryAllow(int studentId);
}
