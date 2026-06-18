using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="SeatLedgerEntry"/> (schema <c>billing</c>), P10-15-BE-11.
///
/// Key decisions:
/// - Append-only: rows are never updated (no soft-delete filter in queries needed, but
///   <see cref="SeatLedgerEntry.DeletedAt"/> comes from <c>FullAuditedEntity</c> and is preserved).
/// - <see cref="SeatLedgerEntry.IdempotencyKey"/> has a unique filtered index (non-null only)
///   to prevent double-appending the same event.
/// - <see cref="SeatLedgerEntry.SubscriptionId"/> is a billing-internal FK.
/// - <see cref="SeatLedgerEntry.ChildId"/> and <see cref="SeatLedgerEntry.ParentUserId"/> are
///   plain <c>int</c> — no cross-module FK (module isolation rule 1).
/// - All timestamps are <c>timestamptz</c> (UTC).
/// </summary>
public sealed class SeatLedgerEntryConfig : IEntityTypeConfiguration<SeatLedgerEntry>
{
    public void Configure(EntityTypeBuilder<SeatLedgerEntry> builder)
    {
        builder.ToTable("SeatLedgerEntries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionId)
            .IsRequired();

        builder.Property(x => x.ParentUserId)
            .IsRequired();

        builder.Property(x => x.ChildId)
            .IsRequired(false);

        builder.Property(x => x.EventType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(18,4)")
            .IsRequired(false);

        builder.Property(x => x.Quantity)
            .IsRequired(false);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(128)
            .IsRequired(false);

        builder.Property(x => x.OccurredAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Billing-internal FK.
        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Standard lookup indexes.
        builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName("IX_SeatLedgerEntries_SubscriptionId");

        builder.HasIndex(x => new { x.SubscriptionId, x.ChildId })
            .HasDatabaseName("IX_SeatLedgerEntries_SubscriptionId_ChildId");

        builder.HasIndex(x => x.OccurredAt)
            .HasDatabaseName("IX_SeatLedgerEntries_OccurredAt");

        // Unique filtered index on IdempotencyKey (prevents double-appending the same event).
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasFilter("\"IdempotencyKey\" IS NOT NULL")
            .HasDatabaseName("UX_SeatLedgerEntries_IdempotencyKey");
    }
}
