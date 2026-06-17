using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Modules.Billing.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="SeatReservation"/> (schema <c>billing</c>), P10-14-BE-2.
///
/// Key decisions:
/// - <see cref="SeatReservation.SubscriptionId"/> is a billing-internal FK to <c>Subscriptions</c>
///   (cascade delete — if a subscription is removed, its seat reservations are too).
/// - <see cref="SeatReservation.ChildId"/> and <see cref="SeatReservation.ParentUserId"/> are
///   plain <c>int</c> columns with NO cross-module FK (module isolation rule 1).
/// - <strong>Unique filtered index</strong> on <c>(SubscriptionId, ChildId) WHERE Status IN (0, 1)</c>
///   (Reserved=0, Active=1) prevents double-occupancy for the same child in the same subscription.
/// - Standard index on <c>SubscriptionId</c> for occupancy-count lookups.
/// - All timestamps are <c>timestamptz</c> (UTC).
/// </summary>
public sealed class SeatReservationConfig : IEntityTypeConfiguration<SeatReservation>
{
    public void Configure(EntityTypeBuilder<SeatReservation> builder)
    {
        builder.ToTable("SeatReservations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SubscriptionId)
            .IsRequired();

        builder.Property(x => x.ChildId)
            .IsRequired();

        builder.Property(x => x.ParentUserId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(SeatStatus.Reserved);

        builder.Property(x => x.ReservedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ReleasedAt)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // Audit columns.
        builder.Property(x => x.CreatedAt).HasColumnType("timestamptz");
        builder.Property(x => x.UpdatedAt).HasColumnType("timestamptz").IsRequired(false);
        builder.Property(x => x.DeletedAt).HasColumnType("timestamptz").IsRequired(false);

        // Billing-internal FK: Subscription → SeatReservation.
        builder.HasOne(x => x.Subscription)
            .WithMany()
            .HasForeignKey(x => x.SubscriptionId)
            .OnDelete(DeleteBehavior.Cascade);

        // Standard index for occupancy-count queries (active seat count per subscription).
        builder.HasIndex(x => x.SubscriptionId)
            .HasDatabaseName("IX_SeatReservations_SubscriptionId");

        // Standard index for parent-scoped seat-status queries.
        builder.HasIndex(x => x.ParentUserId)
            .HasDatabaseName("IX_SeatReservations_ParentUserId");

        // Unique FILTERED index: only one Reserved or Active reservation per (SubscriptionId, ChildId).
        // Status values: Reserved=0, Active=1 (see SeatStatus enum).
        // PostgreSQL filtered index syntax: WHERE "Status" IN (0, 1)
        // This prevents double-occupancy at the DB level; the application layer also guards.
        builder.HasIndex(x => new { x.SubscriptionId, x.ChildId })
            .IsUnique()
            .HasFilter("\"Status\" IN (0, 1)")
            .HasDatabaseName("UX_SeatReservations_SubscriptionId_ChildId_Active");
    }
}
