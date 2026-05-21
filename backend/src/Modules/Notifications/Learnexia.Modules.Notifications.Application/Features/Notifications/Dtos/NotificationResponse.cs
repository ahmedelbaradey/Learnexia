using Learnexia.Modules.Notifications.Domain.Enums;

namespace Learnexia.Modules.Notifications.Application.Features.Notifications.Dtos;

public sealed record NotificationResponse
{
    // Keyed on the opaque Notification.Id (Guid). RecipientExternalUserId is intentionally NOT exposed:
    // the admin caller already supplies recipientUserId in the request, so echoing the enumerable
    // Identity int id back in the body would be an unnecessary disclosure (security audit P4-01 #2).
    public Guid Id { get; init; }
    public string Title { get; init; } = default!;
    public string Body { get; init; } = default!;
    public NotificationType Type { get; init; }
    public bool IsRead { get; init; }
    public DateTime CreatedAtUtc { get; init; }
}
