namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// Overall verdict returned by <see cref="ISafetyLayer.GenerateSafeAsync"/>.
///
/// <para><see cref="Allowed"/> — all checks passed; content is safe for the student.</para>
/// <para><see cref="Blocked"/> — one or more checks returned <see cref="CheckOutcome.Block"/>
/// (or max regeneration attempts exhausted on <see cref="CheckOutcome.NeedsRegeneration"/>).
/// The response content is the child-safe localized fallback, never the unsafe original.</para>
/// <para><see cref="Fallback"/> — the gateway itself failed (unavailable / error) and a
/// pre-configured child-safe static fallback is returned.</para>
/// </summary>
public enum SafetyVerdict
{
    Allowed,
    Blocked,
    Fallback,
}
