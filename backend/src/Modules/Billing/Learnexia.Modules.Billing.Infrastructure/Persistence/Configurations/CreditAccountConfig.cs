using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="CreditAccount"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - <see cref="CreditAccount.ChildId"/> unique (one account per child).
/// - <see cref="CreditAccount.Version"/> maps to <c>xmin</c> Npgsql system column for
///   optimistic-concurrency on debits.
/// - <see cref="CreditAccount.GrantedBalance"/> / <see cref="CreditAccount.PurchasedBalance"/>
///   have DB check constraints to enforce ≥ 0.
/// - P10-04 daily-cap columns folded in (<c>DailyUsed</c>, <c>DailyUsedDateLocal</c>,
///   <c>ChildTimeZoneId</c>) — avoids a second migration in Wave 2.
/// </summary>
public class CreditAccountConfig : IEntityTypeConfiguration<CreditAccount>
{
    public void Configure(EntityTypeBuilder<CreditAccount> builder)
    {
        builder.ToTable("CreditAccounts");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ChildId)
            .IsRequired();

        builder.Property(x => x.GrantedBalance)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.PurchasedBalance)
            .IsRequired()
            .HasDefaultValue(0);

        // DB-level guards: neither balance column can go negative.
        // These are defence-in-depth constraints; the domain entity enforces the same invariant.
        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CreditAccounts_GrantedBalance_NonNegative",
            "\"GrantedBalance\" >= 0"));

        builder.ToTable(t => t.HasCheckConstraint(
            "CK_CreditAccounts_PurchasedBalance_NonNegative",
            "\"PurchasedBalance\" >= 0"));

        builder.Property(x => x.GrantExpiresAtUtc)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // P10-04 daily-cap columns.
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

        // xmin concurrency token — Npgsql-specific row version.
        builder.Property(x => x.Version)
            .IsRowVersion()
            .HasColumnName("xmin")
            .HasColumnType("xid")
            .ValueGeneratedOnAddOrUpdate();

        // Audit columns (from FullAuditedEntity).
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // One account per child.
        builder.HasIndex(x => x.ChildId)
            .IsUnique()
            .HasDatabaseName("IX_CreditAccounts_ChildId");

        // One-to-many with CreditTransaction (within billing schema).
        builder.HasMany(x => x.Transactions)
            .WithOne(t => t.CreditAccount)
            .HasForeignKey(t => t.CreditAccountId)
            .OnDelete(DeleteBehavior.Cascade);
    }
}
