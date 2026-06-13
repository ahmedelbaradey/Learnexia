using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>
/// Pure mapping: (AiTaskKind, AiModelTier?) + config → (providerName, modelId).
/// No I/O. Registered in DI so the routing table is overridable from config.
/// </summary>
public interface IAiModelRouter
{
    /// <summary>
    /// Resolve the provider name and model ID for the given task kind and optional tier hint.
    /// Always returns a valid route — falls back to the configured default provider if no
    /// explicit mapping is found.
    /// </summary>
    RouteResult Route(AiTaskKind taskKind, AiModelTier? tierHint);
}
