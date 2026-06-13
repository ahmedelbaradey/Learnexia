namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Identifies the nature of an AI request. Used by <see cref="AiModelRouter"/> to select
/// the cheapest model tier adequate for the task (NFR-9 cost routing).
/// </summary>
public enum AiTaskKind
{
    /// <summary>A short, fast classification or verification task (cheap tier).</summary>
    CheckAnswer,

    /// <summary>Short binary / categorical classification (cheap tier).</summary>
    Classify,

    /// <summary>Any short, bounded task not covered by the named variants (cheap tier).</summary>
    ShortTask,

    /// <summary>Concept explanation — mid tier (sonnet) at runtime.</summary>
    Explain,

    /// <summary>Pedagogical hint for a student who is stuck — mid tier at runtime.</summary>
    Hint,

    /// <summary>Simplify text or an explanation — mid tier at runtime.</summary>
    Simplify,

    /// <summary>Runtime question generation (mid tier); offline generation uses Batch API.</summary>
    QuestionGeneration,

    /// <summary>Diagram or image analysis — premium tier; used offline or rarely at runtime.</summary>
    AnalyzeDiagram,

    /// <summary>Hard multi-step reasoning — premium tier; offline or rare runtime use.</summary>
    HardReasoning,

    /// <summary>Content quality assurance pass — premium tier; offline only.</summary>
    ContentQa,
}
