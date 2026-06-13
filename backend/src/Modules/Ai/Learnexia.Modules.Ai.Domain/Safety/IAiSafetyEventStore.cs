using Learnexia.Modules.Ai.Domain.Entities;

namespace Learnexia.Modules.Ai.Domain.Safety;

/// <summary>
/// Append-only store for <see cref="SafetyEvent"/> rows, injected into
/// <c>SafetyLayer</c> in the Application layer.
///
/// <para>This thin seam keeps Application from taking a hard reference to
/// <c>AiDbContext</c> (which lives in Infrastructure). The Infrastructure
/// implementation wraps <c>AiDbContext</c> directly.</para>
///
/// <para><strong>Append-only:</strong> no Update or Delete methods exist.
/// The application layer enforces immutability of safety event rows per Q5/Q6.</para>
/// </summary>
public interface IAiSafetyEventStore
{
    /// <summary>
    /// Persist a safety event. This method MUST NOT throw — any internal error
    /// is logged and swallowed so the calling <c>SafetyLayer</c> can still return
    /// the child-safe fallback to the student.
    /// </summary>
    Task AppendAsync(SafetyEvent safetyEvent, CancellationToken ct);
}
