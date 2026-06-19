using Learnexia.Modules.Ai.Application.Abstractions;
using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Ai.Infrastructure.Safety;

/// <summary>
/// Infrastructure implementation of <see cref="IAiUsageLogStore"/>.
/// Wraps <see cref="AiDbContext"/> for append-only <see cref="AiUsageLog"/> persistence.
///
/// <para>Write path: called from <c>AiUsageRecorder</c>'s fire-and-forget background task
/// via a freshly-created DI scope. Append-only; no update or delete.
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only event logs
/// (same pattern as AiSafetyEventStore).</para>
///
/// <para>This class must NOT throw — any exception is caught, logged, and swallowed
/// so the caller can still return the result to the student.</para>
/// </summary>
public sealed class AiUsageLogStore : IAiUsageLogStore
{
    private readonly AiDbContext _db;
    private readonly ILoggerManager _logger;

    public AiUsageLogStore(AiDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AddAsync(AiUsageLog log)
    {
        try
        {
            _db.AiUsageLogs.Add(log);
            // Pass userId = 0 (system-generated telemetry row, no authenticated user in the gateway pipeline).
            await _db.SaveChangesAsync(userId: 0);
        }
        catch (Exception ex)
        {
            // Must NOT propagate — the gateway has already returned its result to the caller.
            _logger.LogError(ex, "AiUsageLogStore: failed to persist AiUsageLog.");
        }
    }
}
