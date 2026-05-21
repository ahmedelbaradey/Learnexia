using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

public sealed class Notification : AuditableEntity<Guid>
{
    private Notification() { }

    public Guid RecipientUserId { get; private set; }
    public string Title { get; private set; } = default!;
    public string Body { get; private set; } = default!;
    public Guid NotificationTypeId { get; private set; }
    public NotificationType? NotificationType { get; private set; }
    public Guid? NotificationModuleId { get; private set; }
    public NotificationModule? NotificationModule { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime? ReadAtUtc { get; private set; }
}
