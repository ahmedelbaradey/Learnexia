namespace Learnexia.Modules.Ai.Application.Services;

/// <summary>The resolved (provider, modelId) pair returned by <see cref="IAiModelRouter"/>.</summary>
public sealed record RouteResult(string ProviderName, string ModelId);
