namespace Learnexia.Modules.Ai.Application.Options;

/// <summary>
/// Configuration options for the Hint intent (P3-05 OQ-5).
///
/// <para>Bound from the <c>Ai:Hint</c> config section (e.g. appsettings.json).
/// Tunable without a code change — raise or lower <see cref="MaxHintLevels"/> to
/// control the escalation ladder before the student is offered a simpler re-explanation.</para>
///
/// <para>Default: 3 levels. After level 3, the SSE response signals the FE to offer the
/// <c>Simplify</c> endpoint ("want a simpler explanation?").</para>
/// </summary>
public sealed class HintOptions
{
    /// <summary>Config section name in appsettings.</summary>
    public const string SectionName = "Ai:Hint";

    /// <summary>
    /// Maximum number of hint escalation levels before offering the Simplify endpoint.
    /// Defaults to 3 (OQ-5 decision). Must be ≥ 1.
    /// </summary>
    public int MaxHintLevels { get; set; } = 3;
}
