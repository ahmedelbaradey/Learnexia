using Learnexia.Modules.Ai.Domain.Entities;

namespace Learnexia.Modules.Ai.Application.Abstractions;

/// <summary>
/// Append-only store for <see cref="AiUsageLog"/> rows, injected into
/// <c>AiUsageRecorder</c> in the Infrastructure layer.
///
/// <para>This thin seam keeps the recorder from taking a hard reference to
/// <c>AiDbContext</c> (which lives in Infrastructure). The Infrastructure
/// implementation wraps <c>AiDbContext</c> directly.</para>
///
/// <para><strong>Append-only:</strong> no Update or Delete methods exist.
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only event logs.</para>
/// </summary>
public interface IAiUsageLogStore
{
    /// <summary>
    /// Persist a usage log entry. Called from a freshly-created DI scope inside
    /// <c>AiUsageRecorder</c>'s fire-and-forget background task.
    /// This method MUST NOT throw — any internal error is logged and swallowed.
    /// </summary>
    Task AddAsync(AiUsageLog log);
}
