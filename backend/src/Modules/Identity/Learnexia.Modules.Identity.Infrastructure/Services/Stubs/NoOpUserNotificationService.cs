using Learnexia.Shared.Contracts.Notifications;

namespace Learnexia.Modules.Identity.Infrastructure.Services.Stubs;

// NOTE: stub. backend/ sends WhatsApp messages via IWhatsAppNotificationService. Replace with a real
// adapter (Notifications module) implementing IUserNotificationService. Until then sends are no-ops.
public sealed class NoOpUserNotificationService : IUserNotificationService
{
    private static readonly NotificationSendResult NotConfigured =
        new(false, null, "User notifications not configured (stub).");

    public Task<NotificationSendResult> SendUserRegistrationMessageAsync(int userId, string phoneNumber, string username, string loginUrl, CancellationToken cancellationToken = default)
        => Task.FromResult(NotConfigured);

    public Task<NotificationSendResult> SendPasswordResetMessageAsync(int userId, string phoneNumber, string temporaryPassword, CancellationToken cancellationToken = default)
        => Task.FromResult(NotConfigured);

    public Task<NotificationSendResult> SendLocalizedMessageAsync(int userId, string phoneNumber, UserMessageType messageType, object[]? parameters = null, CancellationToken cancellationToken = default)
        => Task.FromResult(NotConfigured);
}
