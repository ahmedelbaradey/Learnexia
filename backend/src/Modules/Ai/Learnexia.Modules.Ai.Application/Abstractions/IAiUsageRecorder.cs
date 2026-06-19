using Learnexia.Shared.Contracts.Ai;

namespace Learnexia.Modules.Ai.Application.Abstractions;

/// <summary>
/// Fire-and-forget recorder that persists AI gateway usage telemetry to
/// <c>ai.AiUsageLogs</c> without blocking the hot path.
///
/// <para>The implementation is registered as <b>Singleton</b> and holds only
/// <c>IServiceScopeFactory</c> + <c>ILoggerManager</c>. On each call it spawns
/// a background <c>Task.Run</c> that creates its own DI scope, resolves
/// <see cref="IAiUsageLogStore"/>, and writes the row — so the caller's
/// scoped <c>AiDbContext</c> is never touched after the request scope ends.</para>
///
/// <para>Fail-soft: exceptions are logged and swallowed; the caller is never affected.</para>
/// </summary>
public interface IAiUsageRecorder
{
    /// <summary>
    /// Enqueues a fire-and-forget background write for the supplied usage telemetry.
    /// Returns immediately — the write happens asynchronously in a new DI scope.
    /// </summary>
    /// <param name="usage">Usage telemetry captured by the gateway after a successful call.</param>
    /// <param name="task">The AI task kind that triggered the call.</param>
    /// <param name="studentId">
    /// The student the call was made for. Always <c>null</c> in v1 (the provider-neutral
    /// <c>AiRequest</c> does not carry StudentId); reserved for future enrichment.
    /// </param>
    void Record(AiUsage usage, AiTaskKind task, int? studentId = null);
}
