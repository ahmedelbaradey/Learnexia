using Learnexia.Modules.Ai.Application.Options;
using Learnexia.Shared.Contracts.Ai;
using Microsoft.Extensions.Options;

namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Deterministic, side-effect-free router that maps (AiTaskKind, AiModelTier?) to a
/// (providerName, modelId) pair. The mapping is:
///
///   1. Config lookup: key = "TaskKind:Tier" → exact tier override.
///   2. Config lookup: key = "TaskKind" → task default (any tier).
///   3. Built-in default table (hard-coded per plan Q4, overridable by config).
///   4. Fallback to options.DefaultProvider + built-in model for that provider.
///
/// Built-in defaults (plan routing table):
///   CheckAnswer, Classify, ShortTask                     → Claude  claude-haiku-4-5   (Cheap)
///   Explain, Hint, Simplify, QuestionGeneration          → Claude  claude-sonnet-4-6  (Mid)
///   AnalyzeDiagram, HardReasoning, ContentQa             → Claude  claude-opus-4-8    (Premium)
/// </summary>
public sealed class AiModelRouter : IAiModelRouter
{
    // --- Built-in default table (config overrides these) ---

    private static readonly Dictionary<AiTaskKind, RouteResult> DefaultRoutes =
        new()
        {
            { AiTaskKind.CheckAnswer,        new RouteResult("Claude", "claude-haiku-4-5") },
            { AiTaskKind.Classify,           new RouteResult("Claude", "claude-haiku-4-5") },
            { AiTaskKind.ShortTask,          new RouteResult("Claude", "claude-haiku-4-5") },
            { AiTaskKind.Explain,            new RouteResult("Claude", "claude-sonnet-4-6") },
            { AiTaskKind.Hint,               new RouteResult("Claude", "claude-sonnet-4-6") },
            { AiTaskKind.Simplify,           new RouteResult("Claude", "claude-sonnet-4-6") },
            { AiTaskKind.QuestionGeneration, new RouteResult("Claude", "claude-sonnet-4-6") },
            { AiTaskKind.AnalyzeDiagram,     new RouteResult("Claude", "claude-opus-4-8") },
            { AiTaskKind.HardReasoning,      new RouteResult("Claude", "claude-opus-4-8") },
            { AiTaskKind.ContentQa,          new RouteResult("Claude", "claude-opus-4-8") },
        };

    private readonly AiGatewayOptions _options;

    public AiModelRouter(IOptions<AiGatewayOptions> options)
    {
        _options = options.Value;
    }

    /// <inheritdoc/>
    public RouteResult Route(AiTaskKind taskKind, AiModelTier? tierHint)
    {
        // 1. Exact tier-specific override from config (e.g. "Hint:Mid")
        if (tierHint.HasValue)
        {
            var exactKey = $"{taskKind}:{tierHint.Value}";
            if (_options.Models.TryGetValue(exactKey, out var exactCfg) &&
                !string.IsNullOrWhiteSpace(exactCfg.Provider) &&
                !string.IsNullOrWhiteSpace(exactCfg.ModelId))
            {
                return new RouteResult(exactCfg.Provider, exactCfg.ModelId);
            }
        }

        // 2. Task-level default override from config (e.g. "Hint")
        if (_options.Models.TryGetValue(taskKind.ToString(), out var taskCfg) &&
            !string.IsNullOrWhiteSpace(taskCfg.Provider) &&
            !string.IsNullOrWhiteSpace(taskCfg.ModelId))
        {
            return new RouteResult(taskCfg.Provider, taskCfg.ModelId);
        }

        // 3. Built-in default table
        if (DefaultRoutes.TryGetValue(taskKind, out var builtIn))
        {
            // If the caller asked for a different tier but there's no config override,
            // honour the tier hint by selecting within the built-in Claude models.
            if (tierHint.HasValue)
            {
                var tieredModel = TierToDefaultClaudeModel(tierHint.Value);
                return new RouteResult("Claude", tieredModel);
            }

            return builtIn;
        }

        // 4. Last-resort fallback to options.DefaultProvider + mid-tier Claude model
        return new RouteResult(_options.DefaultProvider, "claude-sonnet-4-6");
    }

    private static string TierToDefaultClaudeModel(AiModelTier tier) => tier switch
    {
        AiModelTier.Cheap   => "claude-haiku-4-5",
        AiModelTier.Mid     => "claude-sonnet-4-6",
        AiModelTier.Premium => "claude-opus-4-8",
        _                   => "claude-sonnet-4-6",
    };
}
