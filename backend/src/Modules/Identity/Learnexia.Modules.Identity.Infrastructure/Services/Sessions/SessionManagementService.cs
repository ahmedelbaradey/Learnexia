using System.Text.Json;
using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Domain.Entities;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Modules.Identity.Domain.Helpers;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.Extensions.Caching.Distributed;

namespace Learnexia.Modules.Identity.Infrastructure.Services.Sessions;

public class SessionManagementService : ISessionManagementService
{
    private readonly IDistributedCache _distributedCache;
    private readonly SessionSettings _sessionSettings;
    private readonly ILoggerManager _logger;

    private const string SESSION_KEY_PREFIX = "session:";
    private const string USER_SESSIONS_KEY_PREFIX = "user_sessions:";
    private const string ACTIVITY_KEY_PREFIX = "activity:";

    public SessionManagementService(IDistributedCache distributedCache, SessionSettings sessionSettings, ILoggerManager logger)
    {
        _distributedCache = distributedCache;
        _sessionSettings = sessionSettings;
        _logger = logger;
    }

    public async Task<UserSession> CreateSessionAsync(int userId, string jwtTokenId, string sessionId)
    {
        try
        {
            var now = DateTime.Now;
            var timeoutMinutes = _sessionSettings.TimeoutMinutes;

            var session = new UserSession
            {
                SessionId = sessionId,
                UserId = userId,
                JwtTokenId = jwtTokenId,
                CreatedAt = now,
                LastActivityAt = now,
                ExpiresAt = now.AddMinutes(timeoutMinutes),
                IsActive = true,
            };

            await StoreSessionAsync(session);
            await AddToUserSessionsAsync(userId, sessionId);
            await UpdateActivityTrackingAsync(sessionId, userId);

            _logger.LogInfo($"Session created for user {userId}: {sessionId}");
            return session;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error creating session for user {userId}");
            throw;
        }
    }

    public async Task<UserSession?> GetSessionAsync(string sessionId)
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(GetSessionKey(sessionId));
            return string.IsNullOrEmpty(json) ? null : JsonSerializer.Deserialize<UserSession>(json);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving session {sessionId}");
            return null;
        }
    }

    // P6-07 OQ-1: read-only validity probe for the OnTokenValidated hot path. Deliberately does NOT
    // swallow store faults (unlike GetSessionAsync above) so the caller can apply an explicit
    // fail-open/fail-closed policy on a Redis/serialization error. A genuinely absent / terminated /
    // expired session returns false (a real revocation → always fail-closed at the caller); only a
    // store/deserialize FAULT throws (→ the caller's env-gated fail-mode). Single GET, zero writes.
    public async Task<bool> IsSessionActiveAsync(string sessionId)
    {
        var json = await _distributedCache.GetStringAsync(GetSessionKey(sessionId));
        if (string.IsNullOrEmpty(json))
            return false;

        var session = JsonSerializer.Deserialize<UserSession>(json);
        return session is { IsActive: true } && !session.IsExpired;
    }

    public async Task<List<UserSession>> GetUserSessionsAsync(int userId)
    {
        try
        {
            var json = await _distributedCache.GetStringAsync(GetUserSessionsKey(userId));
            if (string.IsNullOrEmpty(json))
                return new List<UserSession>();

            var sessionIds = JsonSerializer.Deserialize<List<string>>(json) ?? new List<string>();
            var sessions = new List<UserSession>();
            foreach (var sessionId in sessionIds)
            {
                var session = await GetSessionAsync(sessionId);
                if (session != null && session.IsActive && !session.IsExpired)
                    sessions.Add(session);
            }

            return sessions;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error retrieving sessions for user {userId}");
            return new List<UserSession>();
        }
    }

    public async Task<bool> ExtendSessionAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || !session.IsActive || session.IsExpired)
                return false;

            session.ExtendSession(_sessionSettings.TimeoutMinutes);
            await StoreSessionAsync(session);
            await UpdateActivityTrackingAsync(sessionId, session.UserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error extending session {sessionId}");
            return false;
        }
    }

    public async Task<SessionValidationResponse> ValidateSessionAsync(string sessionId, bool updateActivity = true)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return new SessionValidationResponse { IsValid = false, FailureReason = "Session not found" };

            if (!session.IsActive)
                return new SessionValidationResponse { IsValid = false, FailureReason = "Session is inactive" };

            if (session.IsExpired)
            {
                await TerminateSessionAsync(sessionId, SessionTerminationReason.InactivityTimeout);
                return new SessionValidationResponse { IsValid = false, FailureReason = "Session expired" };
            }

            var wasExtended = false;
            if (_sessionSettings.EnableSlidingExpiration && updateActivity)
            {
                session.LastActivityAt = DateTime.Now;
                session.ExpiresAt = DateTime.Now.AddMinutes(_sessionSettings.TimeoutMinutes);
                await StoreSessionAsync(session);
                await UpdateActivityTrackingAsync(sessionId, session.UserId);
                wasExtended = true;
            }

            return new SessionValidationResponse
            {
                IsValid = true,
                SessionInfo = MapToSessionInfo(session),
                WasExtended = wasExtended,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error validating session {sessionId}");
            return new SessionValidationResponse { IsValid = false, FailureReason = "Validation error" };
        }
    }

    public async Task<bool> TerminateSessionAsync(string sessionId, SessionTerminationReason reason)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null)
                return false;

            session.Terminate(reason.ToString());
            await StoreSessionAsync(session);
            await RemoveFromUserSessionsAsync(session.UserId, sessionId);
            await RemoveActivityTrackingAsync(sessionId);

            _logger.LogInfo($"Session terminated: {sessionId}, Reason: {reason}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error terminating session {sessionId}");
            return false;
        }
    }

    public async Task<SessionInfo?> GetSessionInfoAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            return session != null ? MapToSessionInfo(session) : null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error getting session info for {sessionId}");
            return null;
        }
    }

    public async Task<bool> UpdateSessionActivityAsync(string sessionId)
    {
        try
        {
            var session = await GetSessionAsync(sessionId);
            if (session == null || !session.IsActive)
                return false;

            session.UpdateActivity();
            await StoreSessionAsync(session);
            await UpdateActivityTrackingAsync(sessionId, session.UserId);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error updating session activity for {sessionId}");
            return false;
        }
    }

    public async Task<int> TerminateAllUserSessionsAsync(int userId, SessionTerminationReason reason)
    {
        var terminated = 0;
        try
        {
            var sessions = await GetUserSessionsAsync(userId);
            foreach (var session in sessions)
            {
                try
                {
                    var success = await TerminateSessionAsync(session.SessionId, reason);
                    if (success)
                        terminated++;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Error terminating session {session.SessionId} for user {userId} during terminate-all");
                }
            }

            _logger.LogInfo($"TerminateAllUserSessionsAsync: terminated {terminated} session(s) for user {userId}, reason={reason}");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, $"Error enumerating sessions for user {userId} during terminate-all");
        }

        return terminated;
    }

    private async Task StoreSessionAsync(UserSession session)
    {
        var options = new DistributedCacheEntryOptions { AbsoluteExpiration = session.ExpiresAt };
        await _distributedCache.SetStringAsync(GetSessionKey(session.SessionId), JsonSerializer.Serialize(session), options);
    }

    private async Task AddToUserSessionsAsync(int userId, string sessionId)
    {
        var key = GetUserSessionsKey(userId);
        var existing = await _distributedCache.GetStringAsync(key);
        var sessionIds = string.IsNullOrEmpty(existing)
            ? new List<string>()
            : JsonSerializer.Deserialize<List<string>>(existing) ?? new List<string>();

        if (!sessionIds.Contains(sessionId))
        {
            sessionIds.Add(sessionId);
            await _distributedCache.SetStringAsync(key, JsonSerializer.Serialize(sessionIds));
        }
    }

    private async Task RemoveFromUserSessionsAsync(int userId, string sessionId)
    {
        var key = GetUserSessionsKey(userId);
        var existing = await _distributedCache.GetStringAsync(key);
        if (!string.IsNullOrEmpty(existing))
        {
            var sessionIds = JsonSerializer.Deserialize<List<string>>(existing) ?? new List<string>();
            sessionIds.Remove(sessionId);
            await _distributedCache.SetStringAsync(key, JsonSerializer.Serialize(sessionIds));
        }
    }

    private async Task UpdateActivityTrackingAsync(string sessionId, int userId)
    {
        var activityData = new { SessionId = sessionId, UserId = userId, LastActivity = DateTime.Now, UpdatedAt = DateTime.Now };
        var options = new DistributedCacheEntryOptions { SlidingExpiration = _sessionSettings.SessionTimeout };
        await _distributedCache.SetStringAsync(GetActivityKey(sessionId), JsonSerializer.Serialize(activityData), options);
    }

    private async Task RemoveActivityTrackingAsync(string sessionId)
        => await _distributedCache.RemoveAsync(GetActivityKey(sessionId));

    private static SessionInfo MapToSessionInfo(UserSession session) => new()
    {
        SessionId = session.SessionId,
        IsActive = session.IsActive,
        IsExpired = session.IsExpired,
        ExpiresAt = session.ExpiresAt,
        RemainingSeconds = Math.Max(0, (int)session.RemainingTime.TotalSeconds),
        LastActivityAt = session.LastActivityAt,
        CreatedAt = session.CreatedAt,
    };

    private static string GetSessionKey(string sessionId) => $"{SESSION_KEY_PREFIX}{sessionId}";
    private static string GetUserSessionsKey(int userId) => $"{USER_SESSIONS_KEY_PREFIX}{userId}";
    private static string GetActivityKey(string sessionId) => $"{ACTIVITY_KEY_PREFIX}{sessionId}";
}
