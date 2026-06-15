using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="CreditTransaction"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - <c>UX_CreditTransactions_IdempotencyKey</c> unique constraint — DB-level idempotency guard.
/// - Composite index <c>(CreditAccountId, OccurredAtUtc)</c> for chronological ledger reads.
/// - Index on <c>Type</c> for analytics / reconciliation queries.
/// - Enum columns stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>.
/// - <c>OccurredAtUtc</c> stored as <c>timestamptz</c>.
/// </summary>
public class CreditTransactionConfig : IEntityTypeConfiguration<CreditTransaction>
{
    public void Configure(EntityTypeBuilder<CreditTransaction> builder)
    {
        builder.ToTable("CreditTransactions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreditAccountId).IsRequired();

        builder.Property(x => x.Type)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Pool)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Amount)
            .IsRequired();

        builder.Property(x => x.ReasonCode)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.Reason)
            .IsRequired(false)
            .HasMaxLength(500);

        builder.Property(x => x.ResultingGrantedBalance).IsRequired();
        builder.Property(x => x.ResultingPurchasedBalance).IsRequired();

        // Ledger split columns (W2 carried fix — exact Mixed-pool reconcile).
        builder.Property(x => x.FromGranted)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.FromPurchased)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.OccurredAtUtc)
            .IsRequired()
            .HasColumnType("timestamptz");

        // Idempotency key — unique across all transactions.
        builder.Property(x => x.IdempotencyKey)
            .IsRequired()
            .HasMaxLength(256);

        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_CreditTransactions_IdempotencyKey");

        builder.Property(x => x.RelatedActionId)
            .IsRequired(false)
            .HasMaxLength(128);

        builder.Property(x => x.RelatedPaymentId)
            .IsRequired(false)
            .HasMaxLength(128);

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Composite index for chronological ledger reads.
        builder.HasIndex(x => new { x.CreditAccountId, x.OccurredAtUtc })
            .HasDatabaseName("IX_CreditTransactions_AccountId_OccurredAtUtc");

        // Index on Type for analytics.
        builder.HasIndex(x => x.Type)
            .HasDatabaseName("IX_CreditTransactions_Type");
    }
}
