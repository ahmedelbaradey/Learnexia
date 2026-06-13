using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// The typed verdict returned by a single check implementation
/// (<see cref="IToxicityCheck"/>, <see cref="IAgeAppropriatenessCheck"/>,
/// <see cref="IHallucinationCheck"/>).
///
/// <para><see cref="ReasonCode"/> must be one of the stable string constants in
/// <see cref="ReasonCodes"/> — P7-09/P7-11 categorize <c>ai.SafetyEvents</c> rows
/// by these codes, so they must remain stable.</para>
/// </summary>
/// <param name="Outcome">Whether the content passed, should be blocked, or should be regenerated.</param>
/// <param name="ReasonCode">
/// A stable reason code from <see cref="ReasonCodes"/>.
/// </param>
/// <param name="Score">
/// Optional normalized score in [0,1]. Interpretation is check-specific.
/// Null when the check does not produce a numeric score.
/// </param>
public sealed record CheckVerdict(
    CheckOutcome Outcome,
    string ReasonCode,
    double? Score = null)
{
    /// <summary>
    /// Factory: a clean pass with score <paramref name="score"/>.
    /// </summary>
    public static CheckVerdict Pass(double? score = null) =>
        new(CheckOutcome.Pass, ReasonCodes.Pass, score);

    /// <summary>
    /// Factory: hard block with the given reason code and optional score.
    /// </summary>
    public static CheckVerdict Block(string reasonCode, double? score = null) =>
        new(CheckOutcome.Block, reasonCode, score);

    /// <summary>
    /// Factory: needs regeneration with the given reason code and optional score.
    /// </summary>
    public static CheckVerdict NeedsRegen(string reasonCode, double? score = null) =>
        new(CheckOutcome.NeedsRegeneration, reasonCode, score);
}
