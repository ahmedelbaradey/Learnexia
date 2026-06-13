namespace Learnexia.Shared.Contracts.Ai;

/// <summary>
/// The result of a single named safety check, collected by <see cref="ISafetyLayer"/>
/// and exposed in <see cref="SafeAiResult.Results"/> for analytics and P7-09/P7-11 logging.
///
/// <para>Stable over time — P7-09 (moderation queue) and P7-11 (safety dashboard)
/// categorize events by <see cref="ReasonCode"/>.</para>
/// </summary>
/// <param name="CheckName">
/// Human-readable check name matching the implementing class
/// (e.g. "ToxicityCheck", "AgeAppropriatenessCheck", "HallucinationCheck").
/// </param>
/// <param name="Outcome">The outcome of this check.</param>
/// <param name="ReasonCode">
/// A stable, well-known reason code (e.g. "TOXICITY_HIGH", "PASS", "TOXICITY_CHECK_ERROR").
/// Matches the string set in <c>Ai.Domain/Safety/ReasonCodes.cs</c>.
/// </param>
/// <param name="Score">
/// Optional normalized confidence score in [0,1].
/// Higher = more confident the content is unsafe (for <see cref="CheckOutcome.Block"/>)
/// or safe (for <see cref="CheckOutcome.Pass"/>). Null when not available.
/// </param>
public sealed record CheckResult(
    string CheckName,
    CheckOutcome Outcome,
    string ReasonCode,
    double? Score);
