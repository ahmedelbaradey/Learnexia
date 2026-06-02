using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="UserDeviceToken"/> (P4-09).
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c>.
/// The table lives in the <c>notifications</c> schema (DbContext default schema).
/// <para>
/// <see cref="UserDeviceToken.UserId"/> is a plain int soft-reference to Identity.User — no cross-module FK
/// (module isolation rule 1; CONVENTIONS §12).
/// </para>
/// <para>
/// A unique index on <c>(UserId, ExpoPushToken)</c> prevents duplicate token registrations per user.
/// A non-unique index on <c>UserId</c> supports the "give me all active tokens for this user" inbox query.
/// </para>
/// </summary>
public sealed class UserDeviceTokenConfig : IEntityTypeConfiguration<UserDeviceToken>
{
    public void Configure(EntityTypeBuilder<UserDeviceToken> builder)
    {
        builder.ToTable("UserDeviceTokens", NotificationsDbContext.Schema);

        builder.Property(p => p.UserId).IsRequired();

        builder.Property(p => p.ExpoPushToken).IsRequired().HasMaxLength(256);
        builder.Property(p => p.Platform).IsRequired().HasMaxLength(16);

        builder.Property(p => p.RegisteredAtUtc).IsRequired();
        builder.Property(p => p.LastUsedAtUtc).IsRequired();
        builder.Property(p => p.IsActive).IsRequired();

        // Unique — one row per (user, token). If a token is re-registered by a different user the
        // application layer updates the UserId (token transfer / device rotation).
        builder
            .HasIndex(p => new { p.UserId, p.ExpoPushToken })
            .IsUnique()
            .HasDatabaseName("UX_UserDeviceTokens_UserId_ExpoPushToken");

        // Non-unique index: "give me all active tokens for this user" — inbox query path.
        builder
            .HasIndex(p => p.UserId)
            .HasDatabaseName("IX_UserDeviceTokens_UserId");
    }
}
