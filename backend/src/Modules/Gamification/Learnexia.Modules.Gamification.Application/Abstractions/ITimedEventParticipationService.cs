using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Shared.Contracts.Gamification;
using Learnexia.Shared.Kernel.Responses;

namespace Learnexia.Modules.Gamification.Application.Abstractions;

/// <summary>
/// Service interface for timed-event participation reads (P4-12, BE-8).
/// The <b>accrual logic</b> lives in <c>XpService</c> (inline, behind the XP award path)
/// and does NOT go through this interface — it does not need a service call to be IDOR-safe
/// because the profile row-lock is already held.
///
/// This service provides the student-facing read path consumed by the BE-8 query handler.
/// Impl lives in <c>Infrastructure/Services/TimedEventParticipationService.cs</c>.
/// </summary>
public interface ITimedEventParticipationService
{
    /// <summary>
    /// Returns all active timed-event participation snapshots for <paramref name="studentId"/>
    /// (events whose window has not yet closed). Empty list for brand-new students.
    /// </summary>
    Task<BaseResponse<IReadOnlyList<TimedEventParticipationSnapshot>>> GetActiveParticipationsAsync(
        int studentId, CancellationToken ct = default);
}
