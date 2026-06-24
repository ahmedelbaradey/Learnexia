namespace Learnexia.Modules.Curriculum.Infrastructure.Configuration;

/// <summary>
/// Configuration for <see cref="Jobs.IngestJobAdvanceService"/> (BL-05-BE-13).
/// Bound from the same <c>CurriculumPipeline</c> appsettings section as
/// <see cref="PipelineJobPollerConfiguration"/> — different keys, same section.
///
/// <para>Example appsettings.json entry:
/// <code>
/// "CurriculumPipeline": {
///   "IngestPollerIntervalSeconds": 5,
///   "IngestMaxRetries": 3,
///   "IngestionConfidenceThreshold": 0.7
/// }
/// </code>
/// </para>
/// </summary>
public class IngestJobPollerConfiguration
{
    public const string Section = "CurriculumPipeline";

    /// <summary>
    /// How often <see cref="Jobs.IngestJobAdvanceService"/> wakes to check for Done/Failed ingest jobs.
    /// Default: 5 seconds.
    /// </summary>
    public int IngestPollerIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Maximum number of re-enqueue retries before a job is marked PermanentlyFailed.
    /// After this many attempts, <c>CurriculumDocument.IngestionStatus</c> is set to
    /// <see cref="Learnexia.Modules.Curriculum.Domain.Enums.IngestionStatus.Failed"/>
    /// and <c>IngestionDiagnostics</c> is populated.
    /// Default: 3.
    /// </summary>
    public int IngestMaxRetries { get; init; } = 3;

    /// <summary>
    /// Minimum confidence score (inclusive lower bound) for a Python ingest output item to be
    /// committed directly to the pedagogical tree / chunk store without human review.
    /// Items with confidence below this threshold are routed to
    /// <see cref="Learnexia.Modules.Curriculum.Domain.Entities.IngestionReviewItem"/>
    /// (Q5 decision — BL-05 brief).
    ///
    /// Range: [0.0, 1.0]. Default: 0.7 (70 %).
    /// Tune via CurriculumPipeline:IngestionConfidenceThreshold without redeploying.
    /// </summary>
    public decimal IngestionConfidenceThreshold { get; init; } = 0.7m;
}
