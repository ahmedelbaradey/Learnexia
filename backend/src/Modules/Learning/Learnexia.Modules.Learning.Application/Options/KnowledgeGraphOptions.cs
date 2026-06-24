namespace Learnexia.Modules.Learning.Application.Options;

/// <summary>
/// Configuration options for KnowledgeGraph queries (BL-03-BE-5).
/// Bound from <c>KnowledgeGraph</c> section in appsettings.
///
/// <para>Example appsettings entry:
/// <code>
/// "KnowledgeGraph": { "RemediationMaxDepth": 3 }
/// </code>
/// </para>
/// </summary>
public class KnowledgeGraphOptions
{
    public const string SectionName = "KnowledgeGraph";

    /// <summary>
    /// Maximum BFS depth for remediation traversal (default 3).
    /// Prevents runaway traversal on deep prerequisite chains.
    /// </summary>
    public int RemediationMaxDepth { get; set; } = 3;
}
