using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;

namespace Learnexia.Modules.Identity.Application.Abstractions;

public interface ISessionManagementService
{
    Task<UserSession> CreateSessionAsync(int userId, string jwtTokenId);
    Task<UserSession?> GetSessionAsync(string sessionId);
    Task<List<UserSession>> GetUserSessionsAsync(int userId);
    Task<bool> ExtendSessionAsync(string sessionId);
    Task<SessionValidationResponse> ValidateSessionAsync(string sessionId, bool updateActivity = true);
    Task<bool> TerminateSessionAsync(string sessionId, SessionTerminationReason reason);
    Task<SessionInfo?> GetSessionInfoAsync(string sessionId);
    Task<bool> UpdateSessionActivityAsync(string sessionId);
}
