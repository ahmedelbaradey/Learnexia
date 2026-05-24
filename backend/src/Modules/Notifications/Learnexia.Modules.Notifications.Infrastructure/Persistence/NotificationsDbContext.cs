using Learnexia.Modules.Notifications.Application.Abstractions;
using Learnexia.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence;

public sealed class NotificationsDbContext(DbContextOptions<NotificationsDbContext> options)
    : DbContext(options), INotificationsDbContext
{
    public const string Schema = "notifications";

    public DbSet<Notification> Notifications => Set<Notification>();
    public DbSet<MessageRequest> MessageRequests => Set<MessageRequest>();
    public DbSet<NotificationPreference> NotificationPreferences => Set<NotificationPreference>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(NotificationsDbContext).Assembly);
    }
}
