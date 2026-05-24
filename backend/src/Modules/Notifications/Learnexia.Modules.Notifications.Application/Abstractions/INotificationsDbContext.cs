using Learnexia.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Learnexia.Modules.Notifications.Application.Abstractions;

public interface INotificationsDbContext
{
    DbSet<Notification> Notifications { get; }
    DbSet<MessageRequest> MessageRequests { get; }
    DbSet<NotificationPreference> NotificationPreferences { get; }

    // Exposed so a multi-row upsert (notification preferences, P2-12) can wrap its writes in an EXPLICIT
    // transaction — there is no Unit of Work (ADR 0001), so atomicity across rows is the caller's job.
    DatabaseFacade Database { get; }

    Task<int> SaveChangesAsync(CancellationToken ct = default);
}
