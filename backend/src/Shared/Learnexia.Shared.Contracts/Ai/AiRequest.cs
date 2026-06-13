namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Provider-neutral AI completion request. Created by the prompt builder (P3-03) and consumed
/// by <see cref="IAiGateway"/>. Contains no provider SDK types.
/// </summary>
public sealed record AiRequest
{
    /// <summary>The assembled prompt text (built by P3-03 PromptBuilder).</summary>
    public required string Prompt { get; init; }

    /// <summary>Task kind used by <see cref="AiModelRouter"/> to select the default model tier.</summary>
    public required AiTaskKind Task { get; init; }

    /// <summary>
    /// Optional explicit tier override. When non-null, overrides the default tier for
    /// <see cref="Task"/> (used for escalate-on-failure in the gateway).
    /// </summary>
    public AiModelTier? TierHint { get; init; }

    /// <summary>
    /// Optional conversation history. Null or empty = single-turn completion.
    /// P3-03 populates this for multi-turn tutor sessions.
    /// </summary>
    public IReadOnlyList<AiMessage>? History { get; init; }

    /// <summary>
    /// Optional cacheable system prompt prefix (for Claude prompt-caching).
    /// When set, the gateway marks this with <c>cache_control</c> on the Anthropic API call.
    /// Reduces input-token cost ~90% on repeated calls with the same prefix.
    /// </summary>
    public string? CacheableSystemPrompt { get; init; }

    /// <summary>
    /// Maximum tokens the provider should generate. When null the provider default applies.
    /// </summary>
    public int? MaxTokens { get; init; }
}
