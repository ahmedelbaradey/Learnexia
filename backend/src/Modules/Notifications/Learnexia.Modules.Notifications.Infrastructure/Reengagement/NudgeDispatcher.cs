using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Kernel.Abstractions;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Reengagement;

/// <summary>
/// Default implementation of <see cref="INudgeDispatcher"/> (P4-09 B4-2 / AC4).
///
/// Delivery guarantee order:
/// 1. The in-app <c>Notification</c> row is ALWAYS written (the durable receipt — the bell icon
///    depends on it even when push is suppressed or fails).
/// 2. If <see cref="NudgeMessage.ShouldPush"/> is true and the recipient has active device tokens,
///    <c>IPushSender.SendAsync</c> is called. Push failure is fail-soft: the inbox row is not rolled
///    back; the failure is logged. Invalid tokens are deactivated via <see cref="IDeviceTokenService"/>.
/// 3. <c>DeliveredChannels</c> bitmask is stamped on the row after dispatch: InApp=4, Push=2.
/// 4. The entire method is wrapped in a try/catch — a dispatcher crash does NOT propagate to the
///    integration-event handler caller (ADR 0002 §3).
///
/// Scoped lifetime — one instance per request/handler invocation, owns the scoped DbContext.
/// Uses <see cref="IDeviceTokenService"/> for token lookup/deactivation (no direct DbContext for those).
/// </summary>
public sealed class NudgeDispatcher : INudgeDispatcher
{
    private const int ChannelInApp = 4;
    private const int ChannelPush  = 2;

    private readonly NotificationsDbContext _db;
    private readonly IDeviceTokenService _deviceTokenService;
    private readonly IPushSender _pushSender;
    private readonly ISystemClock _clock;
    private readonly ILoggerManager _logger;

    public NudgeDispatcher(
        NotificationsDbContext db,
        IDeviceTokenService deviceTokenService,
        IPushSender pushSender,
        ISystemClock clock,
        ILoggerManager logger)
    {
        _db                 = db;
        _deviceTokenService = deviceTokenService;
        _pushSender         = pushSender;
        _clock              = clock;
        _logger             = logger;
    }

    public async Task DispatchAsync(NudgeMessage message, CancellationToken ct = default)
    {
        try
        {
            int deliveredChannels = 0;
            var nowUtc = _clock.UtcNow;

            // ── Step 1: In-app inbox row (always, even when ShouldInApp=false — provides a
            //   durable audit record the admin endpoint and analytics can read).
            //   The Channel bit is set only when ShouldInApp is true (parent pref respected).
            var notification = Notification.CreateReengagement(
                recipientExternalUserId: message.RecipientChildUserId,
                category: message.Category,
                code: message.Code,
                title: message.Title,
                body: message.Body,
                dataJson: message.DataJson);

            _db.Notifications.Add(notification);

            if (message.ShouldInApp)
                deliveredChannels |= ChannelInApp;

            // ── Step 2: Push (best-effort; failure does NOT roll back the inbox row).
            if (message.ShouldPush)
            {
                deliveredChannels |= await TrySendPushAsync(message, notification, ct);
            }

            // ── Step 3: Stamp the bitmask + SentAtUtc on the row.
            notification.RecordSent(deliveredChannels, nowUtc);

            await _db.SaveChangesAsync(ct);

            _logger.LogInfo(
                $"analytics.reengagement.sent category={message.Category} " +
                $"childId={message.RecipientChildUserId} code={message.Code} " +
                $"channels={deliveredChannels}");
        }
        catch (Exception ex)
        {
            // Fail-soft: log + swallow so integration-event handler stays healthy.
            _logger.LogError(ex,
                $"P4-09: NudgeDispatcher threw for childId={message.RecipientChildUserId} " +
                $"category={message.Category} — nudge may be missed.");
        }
    }

    // -------------------------------------------------------------------------
    // Private helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Loads active device tokens for the recipient via <see cref="IDeviceTokenService"/>, calls
    /// <see cref="IPushSender"/>, deactivates any invalid tokens returned, and returns the push
    /// channel bitmask (2) if at least one token accepted the message; otherwise 0.
    /// </summary>
    private async Task<int> TrySendPushAsync(
        NudgeMessage message,
        Notification notification,
        CancellationToken ct)
    {
        try
        {
            var activeTokens = await _deviceTokenService.GetActiveTokensAsync(
                message.RecipientChildUserId, ct);

            if (activeTokens.Count == 0)
            {
                _logger.LogInfo(
                    $"P4-09: NudgeDispatcher — no active device tokens for childId={message.RecipientChildUserId}; push skipped.");
                return 0;
            }

            var tokens = activeTokens.Select(t => t.ExpoPushToken).ToList();
            var result = await _pushSender.SendAsync(tokens, message.Title, message.Body, message.DataJson, ct);

            // Deactivate tokens the push provider flagged as invalid (device unregistered / app uninstalled).
            if (result.InvalidTokens.Count > 0)
            {
                var invalidSet  = new HashSet<string>(result.InvalidTokens, StringComparer.Ordinal);
                var invalidIds  = activeTokens
                    .Where(t => invalidSet.Contains(t.ExpoPushToken))
                    .Select(t => t.Id)
                    .ToList();

                if (invalidIds.Count > 0)
                {
                    await _deviceTokenService.DeactivateByIdsAsync(invalidIds, ct);
                    _logger.LogInfo(
                        $"P4-09: NudgeDispatcher — deactivated {invalidIds.Count} invalid token(s) for childId={message.RecipientChildUserId}.");
                }
            }

            if (result.Failed > 0)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.push_partial_failure childId={message.RecipientChildUserId} " +
                    $"sent={result.Sent} failed={result.Failed}");
            }

            return result.Sent > 0 ? ChannelPush : 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P4-09: NudgeDispatcher — push delivery threw for childId={message.RecipientChildUserId}; inbox row is still written.");
            return 0;
        }
    }
}
