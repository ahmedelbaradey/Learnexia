using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Payment"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - No card / PAN / CVV columns — web hosted checkout only.
/// - <see cref="Payment.ParentUserId"/> / <see cref="Payment.TargetChildId"/> are plain ints
///   (no cross-module FK — module isolation rule 1).
/// - <see cref="Payment.SubscriptionId"/> is a billing-internal FK to <c>Subscriptions</c>;
///   <c>OnDelete.SetNull</c> so historical Payment rows survive a Subscription delete.
/// - Unique index on <see cref="Payment.IdempotencyKey"/> prevents duplicate payments.
/// - Index on <see cref="Payment.ProviderPaymentRef"/> for reconciliation lookups.
/// - Index on <c>(ParentUserId, Status)</c> for history queries.
/// - All enum columns stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>.
/// </summary>
public class PaymentConfig : IEntityTypeConfiguration<Payment>
{
    public void Configure(EntityTypeBuilder<Payment> builder)
    {
        builder.ToTable("Payments");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParentUserId).IsRequired();

        builder.Property(x => x.SubscriptionId).IsRequired(false);

        builder.Property(x => x.ProviderPaymentRef)
            .HasMaxLength(512)
            .IsRequired(false);

        builder.Property(x => x.Amount)
            .HasColumnType("numeric(12,2)")
            .IsRequired();

        builder.Property(x => x.Currency)
            .HasMaxLength(3)
            .IsRequired()
            .HasDefaultValue(PaymentCurrency.EGP);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(PaymentStatus.Initiated);

        builder.Property(x => x.Kind)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(PaymentKind.Subscription);

        builder.Property(x => x.TargetChildId).IsRequired(false);

        builder.Property(x => x.IdempotencyKey)
            .HasMaxLength(256)
            .IsRequired();

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Unique: one payment row per idempotency key.
        builder.HasIndex(x => x.IdempotencyKey)
            .IsUnique()
            .HasDatabaseName("UX_Payments_IdempotencyKey");

        // Lookup by provider ref (reconciliation queries).
        builder.HasIndex(x => x.ProviderPaymentRef)
            .HasDatabaseName("IX_Payments_ProviderPaymentRef");

        // History / dunning queries.
        builder.HasIndex(x => new { x.ParentUserId, x.Status })
            .HasDatabaseName("IX_Payments_ParentUserId_Status");

        // Reconcile job: non-terminal old payments.
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Payments_Status");

        // Billing-internal FK: Subscription → Payment (loose — SetNull on delete).
        builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName("IX_Payments_SubscriptionId");
    }
}
