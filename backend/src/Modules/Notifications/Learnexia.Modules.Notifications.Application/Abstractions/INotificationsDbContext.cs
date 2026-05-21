using Learnexia.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<MessageRequest> MessageRequests { get; }
    DbSet<NotificationType> NotificationTypes { get; }
    DbSet<NotificationModule> NotificationModules { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
