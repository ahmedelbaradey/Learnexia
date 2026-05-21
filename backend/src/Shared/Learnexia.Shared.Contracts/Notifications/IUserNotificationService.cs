namespace Learnexia.Shared.Contracts.Notifications;

// Cross-module seam: Identity raises user notifications (registration / password reset) without
// referencing the Notifications module. The Notifications module (or a WhatsApp adapter) implements this.
public interface IUserNotificationService
{
    Task<NotificationSendResult> SendUserRegistrationMessageAsync(int userId, string phoneNumber, string username, string loginUrl, CancellationToken cancellationToken = default);
    Task<NotificationSendResult> SendPasswordResetMessageAsync(int userId, string phoneNumber, string temporaryPassword, CancellationToken cancellationToken = default);
    Task<NotificationSendResult> SendLocalizedMessageAsync(int userId, string phoneNumber, UserMessageType messageType, object[]? parameters = null, CancellationToken cancellationToken = default);
}

public sealed record NotificationSendResult(bool IsSuccess, string? MessageId, string? ErrorMessage);

public enum UserMessageType
{
    RegistrationMessageResend,
}
