using Learnexia.Shared.Kernel.Entities;

namespace Learnexia.Modules.Notifications.Domain.Entities;

public sealed class MessageRequest : AuditableEntity<Guid>
{
    private MessageRequest() { }

    public string Channel { get; private set; } = default!;
    public string Recipient { get; private set; } = default!;
    public string Payload { get; private set; } = default!;
    public string Status { get; private set; } = default!;
    public DateTime? SentAtUtc { get; private set; }
}
