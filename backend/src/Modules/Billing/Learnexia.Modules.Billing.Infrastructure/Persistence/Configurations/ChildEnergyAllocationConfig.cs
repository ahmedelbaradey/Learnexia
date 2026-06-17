using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ChildEnergyAllocation"/> (schema <c>billing</c>, P10-13-BE-4).
///
/// Key decisions:
/// - FK to <see cref="FamilyEnergyAccount"/> (cascade within billing; no cross-module FK).
/// - Unique index on <c>(FamilyEnergyAccountId, ChildId, CycleStartUtc)</c> — one row per child per cycle.
/// - Index on <c>ChildId</c> for the spend hot path (child looks up their own allocation row).
/// - <c>xmin</c> row version for optimistic concurrency on the allocation-first debit.
/// - Non-negative check constraints on <c>AllocatedAmount</c> / <c>SpentAmount</c>.
/// - <c>CycleStartUtc</c> / <c>CycleEndUtc</c> as <c>timestamptz</c>.
/// - <see cref="ChildEnergyAllocation.Remaining"/> is a computed C# property — not persisted.
/// </summary>
public class ChildEnergyAllocationConfig : IEntityTypeConfiguration<ChildEnergyAllocation>
{
    public void Configure(EntityTypeBuilder<ChildEnergyAllocation> builder)
    {
        builder.ToTable("ChildEnergyAllocations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FamilyEnergyAccountId)
            .IsRequired();

        builder.Property(x => x.ChildId)
            .IsRequired();

        builder.Property(x => x.AllocatedAmount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.SpentAmount)
            .IsRequired()
            .HasDefaultValue(0);

        // Remaining is a C# computed property — not a DB column.
        builder.Ignore(x => x.Remaining);

        builder.Property(x => x.CycleStartUtc)
            .IsRequired()
            .HasColumnType("timestamptz");

        builder.Property(x => x.CycleEndUtc)
            .IsRequired()
            .HasColumnType("timestamptz");

        // xmin concurrency token — guards the allocation-first debit hot path.
        builder.Property(x => x.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate();

        // DB-level non-negative guards.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ChildEnergyAllocations_AllocatedAmount_NonNegative",
            "\"AllocatedAmount\" >= 0"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_ChildEnergyAllocations_SpentAmount_NonNegative",
            "\"SpentAmount\" >= 0"));

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // FK to FamilyEnergyAccount (within billing schema — configured on FamilyEnergyAccountConfig).

        // Unique: one allocation row per (family, child, cycle).
        builder.HasIndex(x => new { x.FamilyEnergyAccountId, x.ChildId, x.CycleStartUtc })
            .IsUnique()
            .HasDatabaseName("UX_ChildEnergyAllocations_FamilyChildCycle");

        // Hot-path index: look up by ChildId during spend (allocation-first path).
        builder.HasIndex(x => x.ChildId)
            .HasDatabaseName("IX_ChildEnergyAllocations_ChildId");

        // Navigation: one-to-many with CreditTransaction (per-child allocation rows).
        builder.HasMany<CreditTransaction>()
            .WithOne(t => t.ChildEnergyAllocation)
            .HasForeignKey(t => t.ChildEnergyAllocationId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
