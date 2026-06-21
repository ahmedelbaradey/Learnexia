using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface ISessionManagementService
{
    Task<UserSession> CreateSessionAsync(int userId, string jwtTokenId, string sessionId);
    Task<UserSession?> GetSessionAsync(string sessionId);

    /// <summary>
    /// Read-only validity probe for the per-request auth hot path (P6-07 OnTokenValidated). Returns
    /// <c>true</c> only when the session exists and is active and not expired; <c>false</c> for a
    /// genuinely absent / terminated / expired session (a real revocation). Unlike
    /// <see cref="GetSessionAsync"/> this does NOT swallow store faults — a Redis/serialization error
    /// PROPAGATES so the caller can apply an explicit fail-open/fail-closed policy. Performs no write.
    /// </summary>
    Task<bool> IsSessionActiveAsync(string sessionId);
    Task<List<UserSession>> GetUserSessionsAsync(int userId);
    Task<bool> ExtendSessionAsync(string sessionId);
    Task<SessionValidationResponse> ValidateSessionAsync(string sessionId, bool updateActivity = true);
    Task<bool> TerminateSessionAsync(string sessionId, SessionTerminationReason reason);
    Task<SessionInfo?> GetSessionInfoAsync(string sessionId);
    Task<bool> UpdateSessionActivityAsync(string sessionId);
    Task<int> TerminateAllUserSessionsAsync(int userId, SessionTerminationReason reason);
}
