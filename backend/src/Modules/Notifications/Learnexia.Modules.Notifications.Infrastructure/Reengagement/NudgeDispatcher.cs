using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Services;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Learnexia.Shared.Contracts.Notifications;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Settings;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Reengagement;

/// <summary>
/// Default implementation of <see cref="INudgeDispatcher"/> (P4-09 B4-2 / AC4).
///
/// Delivery guarantee order:
/// 1. The in-app <c>Notification</c> row is ALWAYS written (the durable receipt — the bell icon
///    depends on it even when push is suppressed or fails).
/// 2. If <see cref="NudgeMessage.ShouldPush"/> is true and the recipient has active device tokens,
///    the P9-07 <see cref="INudgeArbiter"/> gate is consulted BEFORE sending:
///    - Global daily push budget: resolved HERE in the dispatcher from the child's reengagement
///      prefs via <see cref="IChildReengagementPreferenceService.GetGlobalDailyPushBudgetAsync"/>,
///      with fallback to the platform config default (<c>Notifications:GlobalDailyPushBudget</c>, 4).
///      Budget is resolved ONCE per push-eligible nudge (only when ShouldPush=true) and is
///      authoritative for ALL handler paths — legacy and new handlers are gated identically.
///    - Per-type cooldown (Redis SETNX, fail-open).
///    Only if the arbiter grants does <c>IPushSender.SendAsync</c> fire.
/// 3. This is the SINGLE choke point for all 11 handler paths (legacy + new). All re-engagement
///    integration-event handlers pass <c>ShouldPush = prefs.Push</c>; the dispatcher resolves
///    the effective budget and applies the global budget + cooldown gate uniformly, so none of
///    the legacy nudges bypass the fire-hose rein.
/// 4. Fail-soft throughout: arbiter failure → fail-open (push proceeds; logged). Push delivery
///    failure → inbox row is NOT rolled back; failure is logged. A dispatcher crash does NOT
///    propagate to the integration-event handler caller (ADR 0002 §3).
/// 5. <c>DeliveredChannels</c> bitmask is stamped on the row after dispatch: InApp=4, Push=2.
///
/// Scoped lifetime — one instance per request/handler invocation, owns the scoped DbContext.
/// Uses <see cref="IDeviceTokenService"/> for token lookup/deactivation (no direct DbContext for those).
/// </summary>
public sealed class NudgeDispatcher : INudgeDispatcher
{
    private const int ChannelInApp = 4;
    private const int ChannelPush  = 2;

    private const string GlobalDailyPushBudgetKey     = "Notifications:GlobalDailyPushBudget";
    private const int    DefaultGlobalDailyPushBudget = 4;

    private readonly NotificationsDbContext                 _db;
    private readonly IChildReengagementPreferenceService    _preferenceService;
    private readonly IDeviceTokenService                    _deviceTokenService;
    private readonly IPushSender                            _pushSender;
    private readonly INudgeArbiter                          _arbiter;
    private readonly IGlobalSettingsProvider                _settings;
    private readonly ISystemClock                           _clock;
    private readonly IPublisher                             _publisher;
    private readonly ILoggerManager                         _logger;

    public NudgeDispatcher(
        NotificationsDbContext db,
        IChildReengagementPreferenceService preferenceService,
        IDeviceTokenService deviceTokenService,
        IPushSender pushSender,
        INudgeArbiter arbiter,
        IGlobalSettingsProvider settings,
        ISystemClock clock,
        IPublisher publisher,
        ILoggerManager logger)
    {
        _db                 = db;
        _preferenceService  = preferenceService;
        _deviceTokenService = deviceTokenService;
        _pushSender         = pushSender;
        _arbiter            = arbiter;
        _settings           = settings;
        _clock              = clock;
        _publisher          = publisher;
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
            //   IMPORTANT: the row is written BEFORE the budget check so the inbox receipt is
            //   durable regardless of push outcome. The budget counter (CountPushesSentTodayAsync)
            //   uses DeliveredChannels & 2 — the push bit is only stamped in Step 3 AFTER the
            //   arbiter grants, so this row does NOT count against the budget until it is
            //   confirmed sent (no double-counting).
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

            // ── Step 2: Push gate — arbiter (global budget + per-type cooldown) + send.
            //   The arbiter is the SINGLE choke point for all 11 handler paths. Handlers set
            //   ShouldPush = prefs.Push only; they do NOT pre-arbitrate. This ensures legacy
            //   nudges (streak-at-risk, hearts-depleted, badge-earned, etc.) are gated equally.
            if (message.ShouldPush)
            {
                var pushGranted = await TryArbiterGrantAsync(message, nowUtc, ct);
                if (pushGranted)
                    deliveredChannels |= await TrySendPushAsync(message, notification, ct);
            }

            // ── Step 3: Stamp the bitmask + SentAtUtc on the row.
            notification.RecordSent(deliveredChannels, nowUtc);

            await _db.SaveChangesAsync(ct);

            _logger.LogInfo(
                $"analytics.reengagement.sent category={message.Category} " +
                $"childId={message.RecipientChildUserId} code={message.Code} " +
                $"channels={deliveredChannels}");

            // ── P9-11: Emit NotificationDispatched analytics event (fail-soft — must NEVER
            //   affect dispatch). Published inline after SaveChangesAsync so deliveredChannels
            //   bitmask is final. Analytics consumer handles idempotency via SourceEventId.
            await PublishDispatchedAsync(message, deliveredChannels, nowUtc, ct);
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
    /// Resolves the effective per-child global daily push budget and consults the P9-07
    /// <see cref="INudgeArbiter"/> for the budget + per-type cooldown gate.
    ///
    /// <para>Budget resolution: the dispatcher looks up the child's stored
    /// <c>GlobalDailyPushBudget</c> via <see cref="IChildReengagementPreferenceService.GetGlobalDailyPushBudgetAsync"/>
    /// and falls back to the platform config default (<c>Notifications:GlobalDailyPushBudget</c>, 4)
    /// only when no per-child value is stored. This ensures the parent's actual setting governs
    /// every nudge — legacy handlers that don't pre-compute the budget are gated identically to
    /// the newer handlers.</para>
    ///
    /// Returns <c>true</c> if the push is granted; <c>false</c> if suppressed.
    /// On exception, fails-open (logs + returns <c>true</c>) so a transient Redis outage does
    /// not silently drop pushes — the DB budget count remains authoritative.
    /// </summary>
    private async Task<bool> TryArbiterGrantAsync(
        NudgeMessage message,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            // Resolve the effective per-child global push budget (dispatcher is the single owner):
            //   1. Look up the child's stored budget from reengagement prefs.
            //   2. Fall back to config default only when no per-child value has been set.
            var perChildBudget = await _preferenceService.GetGlobalDailyPushBudgetAsync(
                message.ParentId, message.RecipientChildUserId, ct);

            var globalBudget = perChildBudget
                ?? _settings.GetInt(GlobalDailyPushBudgetKey, DefaultGlobalDailyPushBudget);

            var result = await _arbiter.ArbitrateAsync(
                message.RecipientChildUserId,
                message.Category,
                message.Code,
                globalBudget,
                nowUtc,
                ct);

            if (!result.ShouldPush)
            {
                _logger.LogInfo(
                    $"analytics.reengagement.push_suppressed reason={result.SuppressReason} " +
                    $"category={message.Category} code={message.Code} " +
                    $"childId={message.RecipientChildUserId}");

                // ── P9-11: Emit NotificationSuppressed analytics event (fail-soft — must NEVER
                //   block dispatch or the inbox write). Suppressed = push channel denied only;
                //   the in-app inbox row is still written (a co-occurring Dispatched will follow).
                await PublishSuppressedAsync(message, result.SuppressReason.ToString(), nowUtc, ct);
            }

            return result.ShouldPush;
        }
        catch (Exception ex)
        {
            // Fail-open: arbiter/Redis failure must NOT break dispatch. Log and allow push
            // through so a transient outage doesn't silently drop nudges. DB budget count
            // remains authoritative — the push bitmask is stamped on commit, so budget
            // is re-enforced on the next event.
            _logger.LogError(ex,
                $"P9-07: NudgeDispatcher — arbiter threw for childId={message.RecipientChildUserId} " +
                $"category={message.Category} code={message.Code} — fail-open, push proceeds.");
            return true;
        }
    }

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

    // ── P9-11: Analytics lifecycle event emitters ─────────────────────────────────────────────

    /// <summary>
    /// Publishes a <see cref="NotificationDispatchedIntegrationEvent"/> in a fail-soft try/catch.
    /// A publish failure MUST NOT affect the dispatch outcome — the inbox row is already persisted.
    /// Mirrors the <c>TimedEventStartedRepublisher</c> fail-soft pattern.
    /// </summary>
    private async Task PublishDispatchedAsync(
        NudgeMessage message,
        int deliveredChannels,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new NotificationDispatchedIntegrationEvent(
                EventId:          Guid.NewGuid(),
                OccurredOnUtc:    nowUtc,
                StudentId:        message.RecipientChildUserId,
                Code:             message.Code,
                Category:         (int)message.Category,
                DeliveredChannels: deliveredChannels), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-11: NudgeDispatcher — failed to publish NotificationDispatchedIntegrationEvent " +
                $"childId={message.RecipientChildUserId} code={message.Code} — analytics sink may miss this event.");
        }
    }

    /// <summary>
    /// Publishes a <see cref="NotificationSuppressedIntegrationEvent"/> in a fail-soft try/catch.
    /// Called when the P9-07 push arbiter denies the push channel (in-app row is still written).
    /// A publish failure MUST NOT affect the dispatch outcome.
    /// </summary>
    private async Task PublishSuppressedAsync(
        NudgeMessage message,
        string suppressReason,
        DateTime nowUtc,
        CancellationToken ct)
    {
        try
        {
            await _publisher.Publish(new NotificationSuppressedIntegrationEvent(
                EventId:       Guid.NewGuid(),
                OccurredOnUtc: nowUtc,
                StudentId:     message.RecipientChildUserId,
                Code:          message.Code,
                Category:      (int)message.Category,
                Reason:        suppressReason), ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                $"P9-11: NudgeDispatcher — failed to publish NotificationSuppressedIntegrationEvent " +
                $"childId={message.RecipientChildUserId} code={message.Code} reason={suppressReason} — analytics sink may miss this event.");
        }
    }
}
