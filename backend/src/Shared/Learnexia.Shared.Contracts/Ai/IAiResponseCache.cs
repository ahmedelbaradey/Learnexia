namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Application-level AI response cache seam (R5 review-gate enforced).
///
/// <para>Only <c>Approved + non-invalidated</c> entries are served as cache hits.
/// Refused / safety-failed / <c>PendingReview</c> / <c>Rejected</c> entries are NEVER
/// returned by <see cref="GetApprovedAsync"/>. This is the core safety property —
/// the cache must not become a bypass of the Safety Layer.</para>
///
/// <para>Implementations provide a Redis hot layer + DB durable read-through.
/// They are responsible for fail-soft behavior (Redis or DB unavailable → treat as MISS,
/// log, continue). They NEVER throw into the calling handler.</para>
///
/// <para>This interface lives in <c>Shared.Contracts</c> so the Ai.Application layer can
/// depend on it without referencing Ai.Infrastructure (module isolation).</para>
/// </summary>
public interface IAiResponseCache
{
    /// <summary>
    /// Attempts to retrieve an approved, non-invalidated cached response for
    /// <paramref name="cacheKey"/>.
    ///
    /// <para>Returns the cached response text when a valid hit is found;
    /// <c>null</c> on a miss (key absent, status ≠ Approved, or invalidated).</para>
    ///
    /// <para>Implementations MUST be fail-soft: any Redis or DB error → return <c>null</c>
    /// (cache miss), log the error, and let the caller proceed to the Safety Layer path.</para>
    /// </summary>
    Task<string?> GetApprovedAsync(string cacheKey, CancellationToken cancellationToken);

    /// <summary>
    /// Persists a safety-passed response to the cache (DB write-through + Redis population).
    ///
    /// <para>The <paramref name="entry"/> carries all R5 gate fields (<c>ReviewStatus</c>
    /// already set to <c>Approved</c> or <c>PendingReview</c> by the calling handler based
    /// on the confidence threshold from <see cref="IGlobalSettingsProvider"/>).</para>
    ///
    /// <para>Fire-and-forget callers must catch all exceptions. Implementations log errors
    /// and never propagate exceptions to the handler that fired and forgot them.</para>
    ///
    /// <para>For <c>WhyWrong</c> entries the implementation enforces the LRU variant cap
    /// (read via <see cref="IGlobalSettingsProvider.GetInt("ai.cache.whyWrongVariantCap", 50)"/>)
    /// before writing.</para>
    /// </summary>
    Task WriteAsync(AiCacheWriteEntry entry, CancellationToken cancellationToken);
}

/// <summary>
/// All data required to write a new cache entry (provided by the calling handler after
/// a safety-PASS response has been produced).
/// </summary>
/// <param name="CacheKey">Pre-computed SHA-256 cache key (full tuple hash).</param>
/// <param name="Response">The safety-passed AI response text to cache.</param>
/// <param name="Type">Entry type discriminator (Explain/Hint/WhyWrong/Practice).</param>
/// <param name="SkillKey">Stable skill identifier for bulk invalidation.</param>
/// <param name="QuestionId">Per-question types only; null for Explain/Practice.</param>
/// <param name="CurriculumVersion">Curriculum version at generation time.</param>
/// <param name="PromptVersion">Prompt template version at generation time.</param>
/// <param name="ModelVersion">Model that generated the response.</param>
/// <param name="ReviewStatus">Approved (confidence ≥ threshold) or PendingReview.</param>
/// <param name="Confidence">Optional model confidence score (0.0–1.0).</param>
public sealed record AiCacheWriteEntry(
    string CacheKey,
    string Response,
    AiCacheEntryTypeDto Type,
    string SkillKey,
    int? QuestionId,
    string CurriculumVersion,
    string PromptVersion,
    string ModelVersion,
    AiCacheReviewStatusDto ReviewStatus,
    decimal? Confidence);

/// <summary>Entry type discriminator (mirrors <c>AiCacheEntryType</c> in Ai.Domain).</summary>
public enum AiCacheEntryTypeDto
{
    Explain  = 1,
    Hint     = 2,
    WhyWrong = 3,
    Practice = 4,
}

/// <summary>Review status (mirrors <c>AiCacheReviewStatus</c> in Ai.Domain).</summary>
public enum AiCacheReviewStatusDto
{
    PendingReview = 0,
    Approved      = 1,
    Rejected      = 2,
}
