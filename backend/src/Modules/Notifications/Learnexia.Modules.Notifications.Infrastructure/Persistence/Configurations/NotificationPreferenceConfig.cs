using Learnexia.Modules.Notifications.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="NotificationPreference"/> (P2-12). Auto-discovered via
/// <c>ApplyConfigurationsFromAssembly</c>. The table lives in the <c>notifications</c> schema (the
/// DbContext default schema). Composite PK (UserId, Category); <see cref="NotificationCategory"/> is
/// stored as its int value. <c>UserId</c> is a plain int with NO foreign-key constraint to the Identity
/// User table (module isolation, rule 1; CONVENTIONS §12).
/// </summary>
public sealed class NotificationPreferenceConfig : IEntityTypeConfiguration<NotificationPreference>
{
    public void Configure(EntityTypeBuilder<NotificationPreference> builder)
    {
        builder.ToTable("NotificationPreferences");

        // The inherited AuditableEntity<int>.Id is unused — (UserId, Category) is the natural key.
        builder.Ignore(p => p.Id);

        builder.HasKey(p => new { p.UserId, p.Category });

        // Reverse-lookup index: "what are this user's preferences?" — the GET/PUT access path.
        builder
            .HasIndex(p => p.UserId)
            .HasDatabaseName("IX_NotificationPreferences_UserId");

        builder.Property(p => p.Category)
            .HasConversion<int>();

        builder.Property(p => p.EmailEnabled).IsRequired();
        builder.Property(p => p.PushEnabled).IsRequired();
    }
}
