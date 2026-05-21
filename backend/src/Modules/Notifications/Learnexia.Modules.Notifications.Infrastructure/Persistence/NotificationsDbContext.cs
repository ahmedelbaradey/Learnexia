using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), INotificationsDbContext
{
    public const string Schema = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<NotificationType> NotificationTypes => Set<NotificationType>();
    public DbSet<NotificationModule> NotificationModules => Set<NotificationModule>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
