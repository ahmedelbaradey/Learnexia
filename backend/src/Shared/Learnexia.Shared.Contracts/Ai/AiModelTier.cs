namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Explicit tier hint the caller may attach to an <see cref="AiRequest"/> to override the
/// router's default tier for a given task kind. When <c>null</c>, the router uses the
/// default configured for the task.
/// </summary>
public enum AiModelTier
{
    /// <summary>Fast, inexpensive model (e.g. claude-haiku-4-5). Default for short tasks.</summary>
    Cheap,

    /// <summary>Mid-tier model (e.g. claude-sonnet-4-6). Default for Explain/Hint/Simplify.</summary>
    Mid,

    /// <summary>Premium, most-capable model (e.g. claude-opus-4-8). Default for hard reasoning.</summary>
    Premium,
}
