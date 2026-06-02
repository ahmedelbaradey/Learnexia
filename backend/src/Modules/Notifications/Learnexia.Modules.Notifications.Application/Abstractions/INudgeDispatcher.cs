using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

/// <summary>
/// Channel-abstracted nudge dispatcher (P4-09 B4-2 / AC4).
/// The single entry-point for sending a re-engagement nudge across all supported channels
/// (in-app inbox + push + web-push). Web-push is deferred to Phase 5; only InApp + Push
/// ship in P4-09.
///
/// <para>Contract guarantees: the in-app inbox row is ALWAYS written — it is the durable receipt.
/// Push delivery is best-effort and never blocks the inbox write. Failures are fail-soft (logged,
/// never rethrown) per ADR 0002 §3.</para>
///
/// Registered as Scoped in <c>AddNotificationsInfrastructure</c>.
/// </summary>
public interface INudgeDispatcher
{
    /// <summary>
    /// Dispatches the nudge described by <paramref name="message"/>. Always writes an in-app
    /// <c>Notification</c> row. Conditionally sends a push notification if
    /// <see cref="NudgeMessage.ShouldPush"/> is true and the recipient has active device tokens.
    /// </summary>
    Task DispatchAsync(NudgeMessage message, CancellationToken ct = default);
}

/// <summary>
/// Immutable value carrying all the data a dispatcher needs to deliver one nudge.
/// </summary>
/// <param name="RecipientChildUserId">Identity int id of the student receiving the nudge.</param>
/// <param name="ParentId">Identity int id of the parent who owns the prefs for this child.</param>
/// <param name="Category">Notification category used to select parent-controlled preference row.</param>
/// <param name="Code">Stable template code, e.g. "BADGE_EARNED" (never user-visible raw string).</param>
/// <param name="Title">Resolved + rendered notification title (locale-specific).</param>
/// <param name="Body">Resolved + rendered notification body (locale-specific).</param>
/// <param name="DataJson">Optional JSON payload (badge code, mission code, etc.). Defaults to "{}".</param>
/// <param name="ShouldPush">Whether the push channel should attempt delivery.</param>
/// <param name="ShouldInApp">Whether the in-app inbox row should be written.</param>
public sealed record NudgeMessage(
    int RecipientChildUserId,
    int ParentId,
    NotificationCategory Category,
    string Code,
    string Title,
    string Body,
    string? DataJson,
    bool ShouldPush,
    bool ShouldInApp);
