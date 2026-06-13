namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The outcome of a single safety check run.
///
/// <para><see cref="Pass"/> — content is safe; allow it through.</para>
/// <para><see cref="Block"/> — content is unsafe; block it and return the child-safe fallback.
/// Any check returning <see cref="Block"/> supersedes <see cref="NeedsRegeneration"/>.</para>
/// <para><see cref="NeedsRegeneration"/> — content is borderline; re-prompt the gateway
/// (bounded by <c>SafetyOptions.MaxRegenerationAttempts</c>) and re-screen.
/// Escalates to <see cref="Block"/> / fallback if max attempts exhausted.</para>
/// </summary>
public enum CheckOutcome
{
    Pass,
    Block,
    NeedsRegeneration,
}
