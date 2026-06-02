using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Features.Reengagement.Dtos;

/// <summary>
/// One notification row in the in-app inbox feed (P4-09 B4-4).
/// <c>Data</c> is a raw JSON string carrying per-nudge payload (badge code, mission code, etc.).
/// The FE parses it and renders the appropriate icon / copy — the backend does not localise here.
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
