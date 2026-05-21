using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

public sealed class NotificationModule : Entity<Guid>
{
    private NotificationModule() { }

    public string Code { get; private set; } = default!;
    public string Name { get; private set; } = default!;
}
