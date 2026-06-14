using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Modules.Ai.Domain.Safety;
using Learnexia.Modules.Ai.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;

namespace Learnexia.Modules.Ai.Infrastructure.Safety;

/// <summary>
/// Infrastructure implementation of <see cref="IAiSafetyEventStore"/>.
/// Wraps <see cref="AiDbContext"/> for append-only <see cref="SafetyEvent"/> persistence.
///
/// <para>Write path: called directly by <c>SafetyLayer.GenerateSafeAsync</c> on every
/// block / regenerate / fallback event. Append-only; no update or delete.
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only event logs
/// (same pattern as AuditLog, ModerationLog).</para>
///
/// <para>This class must NOT throw — any exception is caught, logged, and swallowed
/// so the caller (<c>SafetyLayer</c>) can still return the child-safe fallback
/// to the student.</para>
/// </summary>
public sealed class AiSafetyEventStore : IAiSafetyEventStore
{
    private readonly AiDbContext _db;
    private readonly ILoggerManager _logger;

    public AiSafetyEventStore(AiDbContext db, ILoggerManager logger)
    {
        _db     = db;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task AppendAsync(SafetyEvent safetyEvent, CancellationToken ct)
    {
        try
        {
            _db.SafetyEvents.Add(safetyEvent);
            // Pass userId = 0 (system-generated event, no authenticated user in the safety pipeline).
            await _db.SaveChangesAsync(userId: 0);
        }
        catch (Exception ex)
        {
            // Must NOT propagate — the safety layer blocks the content regardless.
            _logger.LogError(ex, "AiSafetyEventStore: failed to persist SafetyEvent.");
        }
    }
}
