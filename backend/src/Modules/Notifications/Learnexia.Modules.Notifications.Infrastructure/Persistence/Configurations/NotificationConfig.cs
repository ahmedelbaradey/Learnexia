using Learnexia.Modules.Notifications.Domain.Entities;
using Learnexia.Modules.Notifications.Domain.Enums;
using Learnexia.Modules.Notifications.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Notifications.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Notification"/> (extended in P4-09).
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c>.
/// Only the columns that require explicit mapping beyond EF conventions are configured here; the
/// original columns (<c>Id</c>, <c>Title</c>, <c>Body</c>, <c>Type</c>, <c>IsRead</c>, etc.) are
/// resolved by convention and are left untouched.
/// <para>
/// Backfill note: existing rows added before migration <c>AddReengagementInfrastructure</c> receive:
/// <list type="bullet">
///   <item><c>Category = 6</c> (<see cref="NotificationCategory.System"/>) via column DEFAULT in SQL.</item>
///   <item><c>Code = 'SYSTEM'</c> via column DEFAULT in SQL.</item>
///   <item><c>Data = '{}'</c> via column DEFAULT in SQL.</item>
///   <item><c>DeliveredChannels = 0</c> via column DEFAULT in SQL.</item>
///   <item><c>SentAtUtc</c> NULL (nullable column — pre-P4-09 rows have no dispatch record).</item>
/// </list>
/// </para>
/// </summary>
public sealed class NotificationConfig : IEntityTypeConfiguration<Notification>
{
    public void Configure(EntityTypeBuilder<Notification> builder)
    {
        // Category: stored as int, backfill default = 6 (System) for all pre-P4-09 rows.
        // HasDefaultValueSql is used (not HasDefaultValue) because EF Core rejects a CLR-int default on a
        // converted enum property at design time — the SQL literal bypasses the type check.
        // HasSentinel(-1): without it, EF treats the CLR-default 0 (NotificationCategory.WeeklyReport) as
        // "unset" and OMITS the column on insert → Postgres applies the SQL default 6 (System), mis-storing
        // every WeeklyReport notification as System. -1 is not a real category, so EF now always sends the
        // actual value (including 0) while the SQL default still backfills raw/pre-P4-09 inserts.
        builder.Property(p => p.Category)
            .HasConversion<int>()
            .IsRequired()
            .HasSentinel((NotificationCategory)(-1))
            .HasDefaultValueSql("6"); // NotificationCategory.System = 6 (backfill for pre-P4-09 rows only)

        // Code: stable template key, e.g. "BADGE_EARNED". Max 64 chars. Backfill default 'SYSTEM'.
        builder.Property(p => p.Code)
            .IsRequired()
            .HasMaxLength(64)
            .HasDefaultValueSql("'SYSTEM'");

        // Data: per-nudge payload stored as jsonb. Default empty object for pre-P4-09 rows.
        builder.Property(p => p.Data)
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'");

        // DeliveredChannels: bitmask (1=email, 2=push, 4=inApp). Default 0 for pre-P4-09 rows.
        builder.Property(p => p.DeliveredChannels)
            .IsRequired()
            .HasDefaultValue(0);

        // SentAtUtc: nullable — pre-P4-09 rows have no dispatch record.
        builder.Property(p => p.SentAtUtc)
            .IsRequired(false);
    }
}
