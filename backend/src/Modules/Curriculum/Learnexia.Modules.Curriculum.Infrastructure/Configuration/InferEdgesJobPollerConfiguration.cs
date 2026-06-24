namespace Learnexia.Modules.Curriculum.Infrastructure.Configuration;

/// <summary>
/// Configuration for <see cref="Jobs.EdgeInferenceAdvanceService"/> (BL-03-BE-3).
/// Bound from the same <c>CurriculumPipeline</c> appsettings section as
/// <see cref="IngestJobPollerConfiguration"/> — different keys, same section.
///
/// <para>Example appsettings.json entry:
/// <code>
/// "CurriculumPipeline": {
///   "InferEdgesPollerIntervalSeconds": 5,
///   "InferEdgesMaxRetries": 3
/// }
/// </code>
/// </para>
/// </summary>
public class InferEdgesJobPollerConfiguration
{
    public const string Section = "CurriculumPipeline";

    /// <summary>
    /// How often <see cref="Jobs.EdgeInferenceAdvanceService"/> wakes to check for Done/Failed infer_edges jobs.
    /// Default: 5 seconds.
    /// </summary>
    public int InferEdgesPollerIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Maximum number of re-enqueue retries before an infer_edges job is marked PermanentlyFailed.
    /// Default: 3.
    /// </summary>
    public int InferEdgesMaxRetries { get; init; } = 3;
}
