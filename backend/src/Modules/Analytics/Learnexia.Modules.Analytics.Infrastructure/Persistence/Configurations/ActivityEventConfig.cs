using Learnexia.Modules.Analytics.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Analytics.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="ActivityEvent"/>.
///
/// Key decisions (mirrors <see cref="AiUsageLogConfig"/> for the append-only PII-light pattern
/// and <see cref="ModerationItemConfig"/> for the <see cref="ActivityEvent.SourceEventId"/>
/// unique-idempotency index):
/// <list type="bullet">
///   <item>Table <c>analytics.ActivityEvents</c> — explicit schema arg satisfies the
///   <c>HasDefaultSchema("analytics")</c> applied on the context.</item>
///   <item><see cref="ActivityEvent.EventType"/> — required, max 64 chars; stored as plain
///   string for forward-compatibility (mirrors <c>AiUsageLog.TaskKind</c>). Indexed.</item>
///   <item><see cref="ActivityEvent.OccurredAtUtc"/> — <c>timestamptz</c> (UTC). Primary
///   filter column for all DAU/session/retention window queries. Indexed standalone and as
///   part of the composite <c>(StudentId, OccurredAtUtc)</c> index.</item>
///   <item><see cref="ActivityEvent.SourceEventId"/> — required, unique index; prevents
///   double-ingest on event redelivery (mirrors
///   <c>IX_ModerationItems_SourceEventId_Unique</c>).</item>
///   <item>No cross-module FKs: <see cref="ActivityEvent.StudentId"/> is a plain int.</item>
///   <item>Append-only: no Update or Delete pipeline exists.</item>
/// </list>
/// </summary>
public class ActivityEventConfig : IEntityTypeConfiguration<ActivityEvent>
{
    public void Configure(EntityTypeBuilder<ActivityEvent> builder)
    {
        builder.ToTable("ActivityEvents", "analytics");

        builder.HasKey(x => x.Id);

        // ── Core columns ──────────────────────────────────────────────────────────────────────────

        // StudentId — plain int, no cross-module FK (module isolation rule #1).
        builder.Property(x => x.StudentId)
            .IsRequired();

        // EventType — stable string discriminator; max 64 chars.
        builder.Property(x => x.EventType)
            .IsRequired()
            .HasMaxLength(64);

        // SubjectCode — nullable int; usually null in v1.
        builder.Property(x => x.SubjectCode)
            .IsRequired(false);

        // DurationSeconds — nullable int; populated only for duration-bearing events.
        builder.Property(x => x.DurationSeconds)
            .IsRequired(false);

        // OccurredAtUtc — timestamptz. Primary time-range filter column.
        // Legacy-timestamp AppContext switch is enabled at the Host, so DateTime values are
        // accepted regardless of Kind — same rationale as AiUsageLogConfig + SafetyEventConfig.
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        // SourceEventId — uuid, required, unique. Idempotent-ingest dedup key.
        // Mirrors ModerationItem.SourceEventId / ModerationItemConfig.
        builder.Property(x => x.SourceEventId)
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // 1. Unique guard on SourceEventId — idempotency; prevents duplicate rows on event
        //    redelivery. Mirrors IX_ModerationItems_SourceEventId_Unique.
        builder.HasIndex(x => x.SourceEventId)
            .IsUnique()
            .HasDatabaseName("IX_ActivityEvents_SourceEventId_Unique");

        // 2. OccurredAtUtc — primary time-range filter for DAU/session/retention window queries.
        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("IX_ActivityEvents_OccurredAtUtc");

        // 3. Composite (StudentId, OccurredAtUtc) — per-student timeline queries that drive
        //    session derivation and retention (distinct active-days). Most selective pattern.
        builder.HasIndex(x => new { x.StudentId, x.OccurredAtUtc })
            .HasDatabaseName("IX_ActivityEvents_StudentId_OccurredAtUtc");

        // 4. EventType — secondary filter / grouping for per-type aggregations.
        builder.HasIndex(x => x.EventType)
            .HasDatabaseName("IX_ActivityEvents_EventType");

        // 5. Composite (EventType, OccurredAtUtc) — the P9-11 admin notification aggregate filters
        //    on EventType IN (NotificationDispatched/Suppressed/Opened) over a date range. The
        //    existing standalone EventType index (index 4) alone would require a filter-then-sort;
        //    this composite lets Postgres satisfy the WHERE+ORDER BY in a single index scan.
        builder.HasIndex(x => new { x.EventType, x.OccurredAtUtc })
            .HasDatabaseName("IX_ActivityEvents_EventType_OccurredAtUtc");

        // ── P9-11 Notification-analytics facet columns ────────────────────────────────────────────
        // All nullable — every existing producer writes null; only the 3 notification consumers
        // (P9-11) populate them. No cross-module enum reference: Category and Channels are int?,
        // Reason is string?.

        // NotificationCode — nullable string, max 64 chars. Template code group-by/filter key.
        builder.Property(x => x.NotificationCode)
            .IsRequired(false)
            .HasMaxLength(64);

        // NotificationCategory — nullable int. The NotificationCategory enum value (0..9).
        builder.Property(x => x.NotificationCategory)
            .IsRequired(false);

        // NotificationChannels — nullable int. DeliveredChannels bitmask (1=email, 2=push, 4=inApp).
        builder.Property(x => x.NotificationChannels)
            .IsRequired(false);

        // SuppressionReason — nullable string, max 32 chars. P9-07 push-suppression reason name.
        builder.Property(x => x.SuppressionReason)
            .IsRequired(false)
            .HasMaxLength(32);
    }
}
