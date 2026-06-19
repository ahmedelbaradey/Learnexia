using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Shared.Contracts.Ai;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Ai.Infrastructure.Gateway;

/// <summary>
/// Singleton fire-and-forget implementation of <see cref="IAiUsageRecorder"/>.
///
/// <para><strong>Why Singleton:</strong> holds only <c>IServiceScopeFactory</c> (Singleton-safe) and
/// <c>ILoggerManager</c> (Singleton). No scoped services are captured at construction time.</para>
///
/// <para><strong>Fire-and-forget pattern (lead-approved):</strong> <see cref="Record"/> returns immediately
/// and spawns a <c>Task.Run</c> background task. The background task creates its own DI scope so the
/// caller's (request-scoped) <c>AiDbContext</c> is never touched after the request lifetime ends.
/// Exceptions are logged and swallowed (fail-soft) — never propagated to the caller.</para>
///
/// <para><strong>Known limitation (v1):</strong> the <c>StreamAsync</c> path in <c>AiGateway</c>
/// does NOT call <c>EnrichWithCost</c> / <c>LogUsage</c>, so streamed responses are not captured in
/// <c>ai.AiUsageLogs</c> in v1. This is an accepted gap; do not attempt to instrument streaming here
/// until the gateway accumulates streaming usage internally.</para>
/// </summary>
public sealed class AiUsageRecorder : IAiUsageRecorder
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILoggerManager _logger;

    public AiUsageRecorder(IServiceScopeFactory scopeFactory, ILoggerManager logger)
    {
        _scopeFactory = scopeFactory;
        _logger       = logger;
    }

    /// <inheritdoc />
    public void Record(AiUsage usage, AiTaskKind task, int? studentId = null)
    {
        // Fire-and-forget: spawn a background task. The caller returns immediately.
        // A new DI scope is created so we do NOT touch the caller's scoped AiDbContext
        // (the request scope may have ended by the time the background task runs).
        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = _scopeFactory.CreateScope();
                var store = scope.ServiceProvider.GetRequiredService<IAiUsageLogStore>();

                var log = new AiUsageLog
                {
                    Provider           = usage.Provider,
                    ModelId            = usage.ModelId,
                    TaskKind           = task.ToString(),
                    PromptTokens       = usage.PromptTokens,
                    CompletionTokens   = usage.CompletionTokens,
                    EstimatedCostUsd   = usage.EstimatedCostUsd,
                    LatencyMs          = usage.LatencyMs,
                    WasCacheHit        = usage.WasCacheHit,
                    StudentId          = studentId,
                    OccurredAtUtc      = DateTime.UtcNow,
                };

                await store.AddAsync(log);
            }
            catch (Exception ex)
            {
                // Fail-soft: log and swallow — must never affect the caller.
                _logger.LogWarn($"AiUsageRecorder: failed to persist usage (fail-soft): {ex.Message}");
            }
        });
    }
}
