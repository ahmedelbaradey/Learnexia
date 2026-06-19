using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Modules.Moderation.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Moderation.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ModerationItem"/>.
///
/// Key constraints (mirrors <see cref="AuditLogConfig"/> patterns):
/// - Unique index on <see cref="ModerationItem.SourceEventId"/> — idempotency key; prevents duplicate
///   rows on event redelivery. Mirrors <c>AuditLog.EventId</c> unique index.
/// - <see cref="ModerationItem.Source"/> and <see cref="ModerationItem.Status"/> persisted as strings
///   via <c>HasConversion&lt;string&gt;()</c> + maxlen — mirrors <c>SafetyEvent.ActionTaken</c>.
/// - <see cref="ModerationItem.SafetyVerdict"/> stored as <c>jsonb</c> — mirrors
///   <c>SafetyEvent.FailedChecks</c> / <c>ReasonCodes</c>.
/// - <see cref="ModerationItem.DetectedAt"/> and <see cref="ModerationItem.ReviewedAtUtc"/> use
///   column type <c>timestamptz</c> — mirrors <c>AuditLog.OccurredAtUtc</c>.
/// - No cross-module foreign keys: StudentId, ReviewedByUserId are plain ints.
/// </summary>
public class ModerationItemConfig : IEntityTypeConfiguration<ModerationItem>
{
    public void Configure(EntityTypeBuilder<ModerationItem> builder)
    {
        builder.ToTable("ModerationItems");

        builder.HasKey(x => x.Id);

        // ── Idempotency ───────────────────────────────────────────────────────────────────────────

        // Unique index on SourceEventId: the primary guard against double-enqueue on event redelivery.
        // Mirrors IX_AuditLogs_EventId_Unique.
        builder.HasIndex(x => x.SourceEventId)
            .IsUnique()
            .HasDatabaseName("IX_ModerationItems_SourceEventId_Unique");

        builder.Property(x => x.SourceEventId)
            .IsRequired();

        // ── Enum columns (string persistence for stability) ───────────────────────────────────────

        builder.Property(x => x.Source)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50)
            .HasDefaultValue(ModerationStatus.Pending);

        // ── Content reference ─────────────────────────────────────────────────────────────────────

        builder.Property(x => x.ContentReference)
            .IsRequired()
            .HasMaxLength(500);

        // ── Safety verdict (jsonb — mirrors SafetyEvent.FailedChecks / ReasonCodes) ──────────────

        builder.Property(x => x.SafetyVerdict)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'{}'");

        // ── Nullable facet / context fields ──────────────────────────────────────────────────────

        builder.Property(x => x.StudentId)
            .IsRequired(false);

        builder.Property(x => x.TaskKind)
            .IsRequired(false)
            .HasMaxLength(100);

        builder.Property(x => x.SubjectCode)
            .IsRequired(false)
            .HasMaxLength(50);

        builder.Property(x => x.Grade)
            .IsRequired(false);

        // ── UTC timestamps ────────────────────────────────────────────────────────────────────────

        // timestamptz — mirrors AuditLog.OccurredAtUtc. Legacy-timestamp AppContext switch is enabled
        // at the Host so DateTime values are accepted regardless of Kind.
        builder.Property(x => x.DetectedAt)
            .HasColumnType("timestamptz")
            .IsRequired();

        builder.Property(x => x.ReviewedAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired(false);

        // ── Review fields ─────────────────────────────────────────────────────────────────────────

        builder.Property(x => x.ReviewedByUserId)
            .IsRequired(false);

        builder.Property(x => x.ReviewDecisionReason)
            .IsRequired(false)
            .HasMaxLength(2000);

        // ── Query indexes ─────────────────────────────────────────────────────────────────────────

        // Primary paging/filtering index for the admin queue: status + detection time (newest-first).
        builder.HasIndex(x => new { x.Status, x.DetectedAt })
            .HasDatabaseName("IX_ModerationItems_Status_DetectedAt");

        // Source filter (e.g. "show me only AiOutput items").
        builder.HasIndex(x => x.Source)
            .HasDatabaseName("IX_ModerationItems_Source");

        // Time-range filter standalone (dashboard / date-from / date-to).
        builder.HasIndex(x => x.DetectedAt)
            .HasDatabaseName("IX_ModerationItems_DetectedAt");

        // Facet filters for subject + grade combination queries.
        builder.HasIndex(x => new { x.SubjectCode, x.Grade })
            .HasDatabaseName("IX_ModerationItems_SubjectCode_Grade");
    }
}
