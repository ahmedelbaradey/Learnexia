namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Typed, safe error descriptor returned inside <see cref="AiResult"/> on failure.
/// Never contains raw provider error bodies, stack traces, or API keys.
/// </summary>
public sealed record AiError(AiErrorKind Kind, string SafeMessage);
