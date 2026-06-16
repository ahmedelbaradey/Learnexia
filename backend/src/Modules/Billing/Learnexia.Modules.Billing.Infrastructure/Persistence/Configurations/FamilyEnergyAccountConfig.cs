using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="FamilyEnergyAccount"/> (schema <c>billing</c>, P10-13-BE-4).
///
/// Key decisions:
/// - Unique index on <see cref="FamilyEnergyAccount.ParentUserId"/> — one wallet per parent.
/// - <c>xmin</c> row version (Npgsql) for optimistic concurrency on the shared purchased-balance path.
/// - DB check constraints enforce non-negative balances (defence-in-depth).
/// - <c>SubscriptionExpiresAtUtc</c> stored as <c>timestamptz</c>.
/// </summary>
public class FamilyEnergyAccountConfig : IEntityTypeConfiguration<FamilyEnergyAccount>
{
    public void Configure(EntityTypeBuilder<FamilyEnergyAccount> builder)
    {
        builder.ToTable("FamilyEnergyAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParentUserId)
            .IsRequired();

        builder.Property(x => x.SubscriptionBalance)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.PurchasedBalance)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.SubscriptionExpiresAtUtc)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // xmin concurrency token — Npgsql-specific row version for the purchased-fallback guard.
        builder.Property(x => x.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate();

        // DB-level non-negative guards (defence-in-depth; domain entity enforces the same).
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FamilyEnergyAccounts_SubscriptionBalance_NonNegative",
            "\"SubscriptionBalance\" >= 0"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_FamilyEnergyAccounts_PurchasedBalance_NonNegative",
            "\"PurchasedBalance\" >= 0"));

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Unique: one wallet per parent.
        builder.HasIndex(x => x.ParentUserId)
            .IsUnique()
            .HasDatabaseName("UX_FamilyEnergyAccounts_ParentUserId");

        // Navigation: one-to-many with ChildEnergyAllocation.
        builder.HasMany(x => x.Allocations)
            .WithOne(a => a.FamilyEnergyAccount)
            .HasForeignKey(a => a.FamilyEnergyAccountId)
            .OnDelete(DeleteBehavior.Cascade);

        // Navigation: one-to-many with CreditTransaction (wallet-level rows — FamilyEnergyAccountId).
        builder.HasMany<CreditTransaction>()
            .WithOne(t => t.FamilyEnergyAccount)
            .HasForeignKey(t => t.FamilyEnergyAccountId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);
    }
}
