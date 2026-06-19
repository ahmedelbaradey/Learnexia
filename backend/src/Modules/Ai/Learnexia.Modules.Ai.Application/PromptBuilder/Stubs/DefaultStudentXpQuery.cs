using Learnexia.Shared.Contracts.Gamification;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;

/// <summary>
/// Default (stub) implementation of <see cref="IStudentXpQuery"/> registered by the Ai module
/// as a safe cold-start fallback.
///
/// <para>Returns <c>null</c> (no XP profile yet — brand-new student), which the
/// <c>RecommendationNarrationCommandHandler</c> maps to <c>CurrentLevel = 1</c>
/// (the minimum motivational framing, consistent with the seam contract).</para>
///
/// <para><strong>Registration note (P3-14a-BE-4):</strong> registered with
/// <c>TryAddScoped</c> so the real Gamification-registered implementation
/// (<c>CachedStudentXpQuery</c> via <c>AddGamificationInfrastructure</c>) wins when the full
/// modular host is loaded. This stub is used only in Ai-isolated unit tests or minimal
/// integration environments where the Gamification module is not loaded.</para>
/// </summary>
internal sealed class DefaultStudentXpQuery : IStudentXpQuery
{
    /// <inheritdoc/>
    public Task<StudentXpSnapshot?> GetByStudentIdAsync(int studentId, CancellationToken ct = default)
        // Return null — the handler defaults to CurrentLevel = 1 on null (seam contract).
        => Task.FromResult<StudentXpSnapshot?>(null);
}
