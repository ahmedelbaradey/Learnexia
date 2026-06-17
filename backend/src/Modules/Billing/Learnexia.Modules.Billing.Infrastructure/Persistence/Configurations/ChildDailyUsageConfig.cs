using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ChildDailyUsage"/> (schema <c>billing</c>, P10-13-BE-4 / OQ-G).
///
/// Key decisions:
/// - Unique index on <see cref="ChildDailyUsage.ChildId"/> — one row per child.
/// - Survives allocation reset/expiry (separate from <c>ChildEnergyAllocation</c>).
/// - <see cref="ChildDailyUsage.DailyUsed"/> non-negative check constraint.
/// </summary>
public class ChildDailyUsageConfig : IEntityTypeConfiguration<ChildDailyUsage>
{
    public void Configure(EntityTypeBuilder<ChildDailyUsage> builder)
    {
        builder.ToTable("ChildDailyUsages");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChildId)
            .IsRequired();

        builder.Property(x => x.DailyUsed)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.DailyUsedDateLocal)
            .IsRequired(false)
            .HasMaxLength(10); // yyyy-MM-dd

        builder.Property(x => x.ChildTimeZoneId)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValue("Africa/Cairo");

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ChildDailyUsages_DailyUsed_NonNegative",
            "\"DailyUsed\" >= 0"));

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Unique: one row per child.
        builder.HasIndex(x => x.ChildId)
            .IsUnique()
            .HasDatabaseName("UX_ChildDailyUsages_ChildId");
    }
}
