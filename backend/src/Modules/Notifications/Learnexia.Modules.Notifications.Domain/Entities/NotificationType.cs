using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

public sealed class NotificationType : Entity<Guid>
{
    private NotificationType() { }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
}
