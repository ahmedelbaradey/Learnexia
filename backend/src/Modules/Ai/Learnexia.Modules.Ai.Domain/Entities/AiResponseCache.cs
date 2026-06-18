namespace Learnexia.Modules.Ai.Domain.Entities;

/// <summary>
/// Unified AI response cache table (schema <c>ai</c>).
/// Holds pre-generated (offline batch) and runtime-generated (write-through) AI responses
/// for the 4 intents: Explain, Hint, WhyWrong, Practice/SimilarExample.
///
/// <para>Only <see cref="ReviewStatus"/> = <see cref="AiCacheReviewStatus.Approved"/> entries
/// with a null <see cref="InvalidatedAt"/> are served as cache hits (R5 review gate — see
/// <c>ai-cost-routing.md §5</c>). Never cache refuse-and-redirect or safety-failed results.</para>
///
/// <para>No cross-module FKs: <see cref="QuestionId"/> and <see cref="ApprovedBy"/> are plain
/// <c>int</c> columns (module isolation rule 1).</para>
///
/// <para>References: <c>ai-cost-routing.md §5</c> (column spec), P3-01-BE-15, P3-04-BE-8.</para>
/// </summary>
public class AiResponseCache
{
    /// <summary>Surrogate primary key (bigserial / identity).</summary>
    public long Id { get; set; }

    /// <summary>
    /// Stable SHA-256 hash of the canonical key tuple (per-intent).
    /// Lookup is always by this column. Max 512 chars; UNIQUE indexed.
    /// </summary>
    public string CacheKey { get; set; } = null!;

    /// <summary>
    /// The AI-generated and safety-passed response text that is served to students.
    /// Stored as <c>text</c> (unbounded); no PII beyond the curriculum-grounded content.
    /// </summary>
    public string Response { get; set; } = null!;

    /// <summary>
    /// Discriminates between response categories.
    /// Stored as <c>smallint</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// </summary>
    public AiCacheEntryType Type { get; set; }

    /// <summary>
    /// Stable semantic skill identifier (from curriculum versioning).
    /// Enables bulk invalidation per skill.
    /// </summary>
    public string SkillKey { get; set; } = null!;

    /// <summary>
    /// Per-question types only (<see cref="AiCacheEntryType.Hint"/>,
    /// <see cref="AiCacheEntryType.WhyWrong"/>). Plain <c>int</c>, no cross-module FK.
    /// Null for <see cref="AiCacheEntryType.Explain"/> and <see cref="AiCacheEntryType.Practice"/>.
    /// Indexed for targeted per-question invalidation (Decision 4).
    /// </summary>
    public int? QuestionId { get; set; }

    /// <summary>
    /// The <c>CurriculumVersion</c> active at generation time.
    /// Invalidation trigger: archived version → set <see cref="InvalidatedAt"/>.
    /// </summary>
    public string CurriculumVersion { get; set; } = null!;

    /// <summary>
    /// The prompt-template version used to produce this response.
    /// Bump invalidates rows from older prompt versions (R6).
    /// </summary>
    public string PromptVersion { get; set; } = null!;

    /// <summary>
    /// Which model generated the response (e.g. <c>claude-sonnet-4-6</c>).
    /// </summary>
    public string ModelVersion { get; set; } = null!;

    /// <summary>
    /// R5 review gate. Only <see cref="AiCacheReviewStatus.Approved"/> entries are served.
    /// Stored as <c>smallint</c> via <c>HasConversion&lt;int&gt;()</c>.
    /// </summary>
    public AiCacheReviewStatus ReviewStatus { get; set; } = AiCacheReviewStatus.PendingReview;

    /// <summary>
    /// Optional model-reported confidence (0.0–1.0). Drives auto-approval:
    /// <c>Confidence ≥ IGlobalSettingsProvider.GetDecimal("ai.cache.autoApprovalConfidence", 0.85m)</c>
    /// → <see cref="AiCacheReviewStatus.Approved"/>; otherwise <see cref="AiCacheReviewStatus.PendingReview"/>.
    /// </summary>
    public decimal? Confidence { get; set; }

    /// <summary>UTC time the row was created (set by the cache-write path).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>
    /// Admin userId who explicitly approved the row (plain <c>int</c>, no cross-module FK).
    /// Null when auto-approved by confidence threshold.
    /// </summary>
    public int? ApprovedBy { get; set; }

    /// <summary>UTC time of explicit approval. Null when auto-approved or pending.</summary>
    public DateTime? ApprovedAt { get; set; }

    /// <summary>
    /// Set when the row is soft-invalidated (R6 triggers). Invalidated rows are not served
    /// as cache hits but are retained for audit.
    /// </summary>
    public DateTime? InvalidatedAt { get; set; }
}

/// <summary>
/// Discriminator for <see cref="AiResponseCache"/> rows (stored as <c>smallint</c>).
/// </summary>
public enum AiCacheEntryType : short
{
    Explain        = 1,
    Hint           = 2,
    WhyWrong       = 3,
    Practice       = 4,
    // P3-14 Lexi recommendation narration (lead-approved 2026-06-18).
    Recommendation = 5,
}

/// <summary>
/// R5 review gate values for <see cref="AiResponseCache"/> rows (stored as <c>smallint</c>).
/// Only <see cref="Approved"/> entries are served as cache hits.
/// </summary>
public enum AiCacheReviewStatus : short
{
    PendingReview = 0,
    Approved      = 1,
    Rejected      = 2,
}
