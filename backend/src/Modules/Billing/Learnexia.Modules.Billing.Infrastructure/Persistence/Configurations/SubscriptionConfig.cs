using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Subscription"/> (schema <c>billing</c>).
///
/// Key decisions:
/// - <see cref="Subscription.ParentUserId"/> is a plain <c>int</c> — no cross-module FK
///   (module isolation rule 1).
/// - Filtered unique index on <c>ParentUserId WHERE Status = Active</c> enforces
///   one active subscription per parent (family-plan invariant).
/// - All enum columns stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>.
/// - Cycle timestamps use <c>timestamptz</c> (UTC).
/// </summary>
public class SubscriptionConfig : IEntityTypeConfiguration<Subscription>
{
    public void Configure(EntityTypeBuilder<Subscription> builder)
    {
        builder.ToTable("Subscriptions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ParentUserId)
            .IsRequired();

        builder.Property(x => x.PlanCode)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(PlanCode.Free);

        builder.Property(x => x.BillingPeriod)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(BillingPeriod.Monthly);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(SubscriptionStatus.Active);

        builder.Property(x => x.CurrentCycleStart)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(x => x.CurrentCycleEnd)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(x => x.PendingPlanCode)
            .HasConversion<int?>()
            .IsRequired(false);

        builder.Property(x => x.PendingBillingPeriod)
            .HasConversion<int?>()
            .IsRequired(false);

        // ── P10-14: Extra seats purchased add-on ─────────────────────────────────────
        builder.Property(x => x.PurchasedExtraSeats)
            .IsRequired()
            .HasDefaultValue(0);

        // ── P10-14-BE-8: scheduled (cycle-end) extra-seat cancellation marker ─────────
        builder.Property(x => x.PendingExtraSeatRemovals)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.ExtraSeatCancelEffectiveAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // ── P10-09: Dunning fields ────────────────────────────────────────────────────
        builder.Property(x => x.FailedAttemptCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.NextRetryAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(x => x.GraceEndsAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // ── P10-15-BE-1: Seat grace audit fields ─────────────────────────────────────
        builder.Property(x => x.SeatGraceStartedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        builder.Property(x => x.SeatGraceReason)
            .HasConversion<int?>()
            .IsRequired(false);

        // Index to support DunningRetryJob's query: WHERE Status IN (PastDue, Dunning) AND NextRetryAt <= now
        builder.HasIndex(x => new { x.Status, x.NextRetryAt })
            .HasDatabaseName("IX_Subscriptions_Status_NextRetryAt");

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Filtered unique: only ONE Active subscription per parent at a time.
        // PendingPayment, Canceled, Downgrading rows can coexist (history).
        builder.HasIndex(x => x.ParentUserId)
            .IsUnique()
            .HasFilter("\"Status\" = 0")   // 0 = SubscriptionStatus.Active
            .HasDatabaseName("UX_Subscriptions_ParentUserId_Active");

        // Fast lookup by parent.
        builder.HasIndex(x => x.ParentUserId)
            .HasDatabaseName("IX_Subscriptions_ParentUserId");

        // Status index (grant job + tier resolution scans Active rows).
        builder.HasIndex(x => x.Status)
            .HasDatabaseName("IX_Subscriptions_Status");
    }
}
