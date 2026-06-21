using Learnexia.Modules.Identity.Application.Abstractions;
using Learnexia.Modules.Identity.Application.Events;
using Learnexia.Modules.Identity.Domain.Enums;
using Learnexia.Shared.Contracts.Admin;
using Learnexia.Shared.Contracts.Identity;
using Learnexia.Shared.Kernel.Abstractions;
using MediatR;
using Microsoft.Extensions.Caching.Distributed;

namespace Learnexia.Modules.Identity.Infrastructure.Events;

/// <summary>
/// Post-commit handler for <see cref="AccountDeletedDomainEvent"/> (P7-07 Security High #1 fix).
///
/// Dispatched by <c>UnitOfWorkBehavior</c> after <c>CommitAsync</c> via the
/// <c>IIdentityDomainEventsBuffer</c> drain path. Because it runs strictly post-commit:
///   — session revocation targets a genuinely committed soft-delete (no phantom revocations on rollback).
///   — <see cref="AccountDeletedIntegrationEvent"/> consumers never react to uncommitted state.
///   — the audit record (<see cref="AdminActionPerformedEvent"/>) reflects an actually committed action.
///
/// Fail-soft per ADR 0002 §3: all side-effects are individually caught + logged. A failure here must
/// NOT propagate back to the UoW (which would 500 an already-committed and already-successful delete).
/// The UoW wrapper also catches at the dispatch level for the same reason.
///
/// Auto-registered by the MediatR host-level scan (Host registers all Application + Infrastructure
/// assembly references; INotificationHandler resolution is assembly-agnostic).
/// </summary>
public sealed class AccountDeletedDomainEventHandler
    : INotificationHandler<AccountDeletedDomainEvent>
{
    private readonly ISessionManagementService _sessionManagementService;
    private readonly IDistributedCache _distributedCache;
    private readonly IPublisher _publisher;
    private readonly ILoggerManager _logger;

    public AccountDeletedDomainEventHandler(
        ISessionManagementService sessionManagementService,
        IDistributedCache distributedCache,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _sessionManagementService = sessionManagementService;
        _distributedCache = distributedCache;
        _publisher = publisher;
        _logger = logger;
    }

    public async Task Handle(AccountDeletedDomainEvent notification, CancellationToken ct)
    {
        // ── 1. Revoke sessions for the primary account ────────────────────────
        await RevokeUserSessionsAsync(notification.UserId, ct);

        // ── 2. Revoke sessions for each cascaded child ────────────────────────
        foreach (var childId in notification.AffectedChildIds)
            await RevokeUserSessionsAsync(childId, ct);

        // ── 3. Publish AccountDeletedIntegrationEvent (best-effort) ──────────
        try
        {
            await _publisher.Publish(
                new AccountDeletedIntegrationEvent(
                    EventId: notification.EventId,
                    OccurredOnUtc: notification.OccurredOnUtc,
                    UserId: notification.UserId,
                    DeletedByAdminUserId: notification.DeletedByAdminUserId,
                    AffectedChildIds: notification.AffectedChildIds),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: failed to publish AccountDeletedIntegrationEvent for userId={notification.UserId}" +
                $" eventId={notification.EventId} — downstream consumers may be missed. Primary mutation committed.");
        }

        // ── 4. Publish AdminActionPerformedEvent (audit, best-effort) ─────────
        try
        {
            await _publisher.Publish(
                new AdminActionPerformedEvent(
                    EventId: Guid.NewGuid(),
                    OccurredAtUtc: notification.OccurredOnUtc,
                    AdminUserId: notification.DeletedByAdminUserId,
                    Action: AdminActions.AccountDeleted,
                    TargetEntityType: "User",
                    TargetEntityId: notification.UserId,
                    Details: notification.AuditDetails),
                ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: failed to publish AdminActionPerformedEvent (audit) for userId={notification.UserId}" +
                $" eventId={notification.EventId} — audit row may be missed. Primary mutation committed.");
        }
    }

    // ── Helpers ───────────────────────────────────────────────────────────────

    private async Task RevokeUserSessionsAsync(int userId, CancellationToken ct)
    {
        // Remove the Redis refresh token.
        try
        {
            await _distributedCache.RemoveAsync($"userrefreshtoken-{userId}", ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: failed to revoke refresh token for userId={userId} (post-commit delete) — ignored.");
        }

        // Terminate all tracked sessions via the new P6-07 helper (DRY).
        try
        {
            var count = await _sessionManagementService.TerminateAllUserSessionsAsync(userId, SessionTerminationReason.AdminTermination);
            _logger.LogInfo($"P7-07: terminated {count} session(s) for userId={userId} (post-commit delete).");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P7-07: failed to terminate sessions for userId={userId} (post-commit delete) — ignored.");
        }
    }
}
