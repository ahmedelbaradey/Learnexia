namespace Learnexia.Modules.Curriculum.Infrastructure.Configuration;

/// <summary>
/// Configuration for <c>ParseJobAdvanceService</c> (BL-02-BE-7, INFRA-4).
/// Bound from the <c>CurriculumPipeline</c> appsettings section.
///
/// <para>Example appsettings.json entry:
/// <code>
/// "CurriculumPipeline": {
///   "PollerIntervalSeconds": 5,
///   "MaxRetries": 3
/// }
/// </code>
/// </para>
/// </summary>
public class PipelineJobPollerConfiguration
{
    public const string Section = "CurriculumPipeline";

    /// <summary>
    /// How often the advance poller wakes to check for Done/Failed parse jobs.
    /// Default: 5 seconds.
    /// </summary>
    public int PollerIntervalSeconds { get; init; } = 5;

    /// <summary>
    /// Maximum number of re-enqueue retries before a job is marked PermanentlyFailed.
    /// After this many attempts, <c>CurriculumDocument.Status</c> is set to
    /// <see cref="Learnexia.Modules.Curriculum.Domain.Enums.DocumentStatus.Failed"/>
    /// and the document's <c>StatusReason</c> is populated.
    /// Default: 3.
    /// </summary>
    public int MaxRetries { get; init; } = 3;
}
