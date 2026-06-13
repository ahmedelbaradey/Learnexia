using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.PromptBuilder.Stubs;

/// <summary>
/// Default (stub) implementation of <see cref="IStudentWeakAreasQuery"/>.
/// Always returns an empty list — enables P3-03 to ship before P3-09 (student mastery
/// / weak-area computation) is implemented (AC4 graceful degradation).
///
/// <para>P3-09 overrides this registration with the real implementation.
/// The prompt builder handles empty lists by omitting the weak-areas section entirely
/// (no empty-header artifact).</para>
/// </summary>
internal sealed class EmptyWeakAreasQuery : IStudentWeakAreasQuery
{
    public Task<IReadOnlyList<WeakArea>> GetWeakAreasAsync(
        int studentId,
        Subject subject,
        CancellationToken ct = default)
        => Task.FromResult<IReadOnlyList<WeakArea>>(Array.Empty<WeakArea>());
}
