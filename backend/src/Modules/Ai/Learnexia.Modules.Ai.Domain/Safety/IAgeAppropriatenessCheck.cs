namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// Checks AI-generated content for adult material, violence, or any themes that are
/// inappropriate for school-age children (under-13 primary audience).
///
/// <para><strong>Fail-closed contract:</strong> on any error, timeout, or unexpected exception
/// the implementation MUST return <see cref="CheckVerdict.Block"/> with reason code
/// <see cref="ReasonCodes.AgeCheckError"/>. Returning <see cref="CheckVerdict.Pass"/>
/// when the check cannot complete is a security violation (FR-AI-4, AC3).</para>
///
/// <para>Implemented by <c>AgeAppropriatenessCheck</c> in <c>Ai.Infrastructure/Safety/</c>.</para>
/// </summary>
public interface IAgeAppropriatenessCheck
{
    /// <summary>
    /// Evaluate <paramref name="content"/> for age-appropriateness in the given <paramref name="language"/>.
    /// </summary>
    /// <param name="content">The AI-generated text to evaluate. Never null.</param>
    /// <param name="language">Content language code, e.g. <c>"ar"</c> or <c>"en"</c>.</param>
    /// <param name="ct">Cancellation token. On cancellation, return <see cref="CheckVerdict.Block"/> (fail-closed).</param>
    /// <returns>A <see cref="CheckVerdict"/> — never throws.</returns>
    Task<CheckVerdict> CheckAsync(string content, string language, CancellationToken ct);
}
