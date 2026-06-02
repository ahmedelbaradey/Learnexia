using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ChildReengagementPreference"/> (P4-09).
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c>.
/// The table lives in the <c>notifications</c> schema (DbContext default schema).
/// <para>
/// PK is the inherited <c>int Id</c> from <see cref="Learnexia.Shared.Kernel.Entities.FullAuditedEntity"/>.
/// A unique index on <c>(ParentId, ChildId, Category)</c> enforces the "at most one row per (parent, child,
/// category)" invariant without making those columns a composite PK.
/// </para>
/// <para>
/// <c>ParentId</c> and <c>ChildId</c> are plain ints with NO foreign-key constraint to Identity schema
/// (module isolation rule 1; CONVENTIONS §12).
/// </para>
/// </summary>
public sealed class ChildReengagementPreferenceConfig : IEntityTypeConfiguration<ChildReengagementPreference>
{
    public void Configure(EntityTypeBuilder<ChildReengagementPreference> builder)
    {
        builder.ToTable("ChildReengagementPreferences", NotificationsDbContext.Schema);

        builder.Property(p => p.ParentId).IsRequired();
        builder.Property(p => p.ChildId).IsRequired();

        builder.Property(p => p.Category)
            .HasConversion<int>()
            .IsRequired();

        // F-02 security fix: conservative opt-in defaults — Email and Push default to false
        // so a direct DB INSERT cannot activate push without explicit parent consent (COPPA / AC3).
        // InApp stays true (in-app is not a consent-gated channel).
        builder.Property(p => p.Email).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.Push).IsRequired().HasDefaultValue(false);
        builder.Property(p => p.InApp).IsRequired().HasDefaultValue(true);

        builder.Property(p => p.QuietHoursStartLocal).HasColumnType("time").IsRequired();
        builder.Property(p => p.QuietHoursEndLocal).HasColumnType("time").IsRequired();

        builder.Property(p => p.TimeZoneId).IsRequired().HasMaxLength(64);

        builder.Property(p => p.DailyCap).IsRequired().HasDefaultValue(3);

        // Unique index — the parent/child/category combination must be unique.
        builder
            .HasIndex(p => new { p.ParentId, p.ChildId, p.Category })
            .IsUnique()
            .HasDatabaseName("UX_ChildReengagementPreferences_ParentId_ChildId_Category");
    }
}
