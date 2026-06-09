using Learnexia.Modules.Moderation.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Moderation.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="AuditLog"/>.
///
/// Key constraints:
/// - Unique index on <see cref="AuditLog.EventId"/> — idempotency key; prevents duplicates on redelivery.
/// - <see cref="AuditLog.OccurredAtUtc"/> uses column type <c>timestamptz</c> (timestamp with time zone).
///   With the legacy-timestamp AppContext switch enabled at the Host, DateTime values are stored/retrieved
///   as-is without Kind conversion; values should be UTC at the produce side (the event carriers do this).
/// - <see cref="AuditLog.Action"/> and <see cref="AuditLog.TargetEntityType"/> are non-nullable strings
///   with a reasonable max length.
/// - No cross-module foreign keys: AdminUserId and TargetEntityId are plain ints.
/// </summary>
public class AuditLogConfig : IEntityTypeConfiguration<AuditLog>
{
    public void Configure(EntityTypeBuilder<AuditLog> builder)
    {
        builder.ToTable("AuditLogs");

        builder.HasKey(x => x.Id);

        // Idempotency unique index — the primary guard against duplicate rows on event redelivery.
        builder.HasIndex(x => x.EventId)
            .IsUnique()
            .HasDatabaseName("IX_AuditLogs_EventId_Unique");

        // Performance indexes for the filtered read query (P7-12 §Read side).
        builder.HasIndex(x => x.AdminUserId)
            .HasDatabaseName("IX_AuditLogs_AdminUserId");

        builder.HasIndex(x => x.Action)
            .HasDatabaseName("IX_AuditLogs_Action");

        builder.HasIndex(new[] { nameof(AuditLog.TargetEntityType), nameof(AuditLog.TargetEntityId) })
            .HasDatabaseName("IX_AuditLogs_TargetEntityType_TargetEntityId");

        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("IX_AuditLogs_OccurredAtUtc");

        // Column type constraints.
        builder.Property(x => x.EventId)
            .IsRequired();

        builder.Property(x => x.AdminUserId)
            .IsRequired();

        builder.Property(x => x.Action)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TargetEntityType)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.TargetEntityId)
            .IsRequired();

        builder.Property(x => x.Details)
            .HasMaxLength(2000);

        // timestamptz — UTC timestamp. Npgsql legacy-timestamp switch is enabled at the Host, so
        // DateTime values are accepted regardless of Kind.
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();
    }
}
