using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;

/// <summary>
/// One notification row in the in-app inbox feed (P4-09 B4-4).
/// <c>Data</c> is a raw JSON string carrying per-nudge payload (badge code, mission code, etc.).
/// The FE parses it and renders the appropriate icon / copy — the backend does not localise here.
///
/// P9-10 (BE-4) — Localization policy (send-time v1):
/// Title and Body are written in the recipient's <c>PreferredLanguage</c> at send time (the same locale
/// used by all re-engagement handlers via <see cref="ReengagementHandlerHelper.GetLocaleAsync"/>).
/// The read path returns the stored, frozen Title/Body verbatim — no re-rendering on read.
/// Forward target: re-localize on read from Code+Data so a language switch retranslates history.
/// That upgrade is owned by / sequenced with the P9-03 FE inbox consumer. Until P9-03 ships, a user
/// who switches language will see older notifications in the language they had at send time — accepted
/// trade-off for v1 (matches existing re-engagement behavior; Code+Data columns are already in place).
/// </summary>
public sealed class InboxItemDto
{
    public Guid Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public NotificationCategory Category { get; set; }
    public string Code { get; set; } = string.Empty;
    public string? Data { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAtUtc { get; set; }
    public DateTime? OpenedAtUtc { get; set; }
}

/// <summary>Paged wrapper for inbox items.</summary>
public sealed class PagedInboxResult
{
    public List<InboxItemDto> Items { get; set; } = new();
    public int TotalCount { get; set; }
    public int Skip { get; set; }
    public int Take { get; set; }
}
