using Learnexia.Modules.Ai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Ai.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="AiUsageLog"/>.
///
/// Key decisions:
/// - <see cref="AiUsageLog.StudentId"/> is a plain <c>int?</c> — nullable, no cross-module FK (module isolation rule 1).
///   Always null in v1; reserved for future enrichment without a schema change.
/// - <see cref="AiUsageLog.EstimatedCostUsd"/> uses column type <c>numeric(12,6)</c> — six decimal places
///   matches the gateway <c>:F6</c> log format; not money type (per CONVENTIONS).
/// - <see cref="AiUsageLog.OccurredAtUtc"/> uses column type <c>timestamptz</c> (UTC) and is indexed
///   as the primary filter for P7-11 time-range dashboard queries. Mirrors <c>IX_SafetyEvents_OccurredAtUtc</c>.
/// - <see cref="AiUsageLog.ModelId"/> and <see cref="AiUsageLog.TaskKind"/> are indexed for breakdown
///   aggregations on the cost dashboard.
/// - No jsonb columns: all fields are scalar (unlike SafetyEvent which has FailedChecks/ReasonCodes jsonb).
/// - Append-only: no Update or Delete command, handler, or endpoint exists.
/// </summary>
public class AiUsageLogConfig : IEntityTypeConfiguration<AiUsageLog>
{
    public void Configure(EntityTypeBuilder<AiUsageLog> builder)
    {
        builder.ToTable("AiUsageLogs", "ai");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Provider)
            .IsRequired()
            .HasMaxLength(100);

        // ModelId — indexed for breakdown-by-model queries on the cost dashboard.
        builder.Property(x => x.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        // TaskKind — indexed for breakdown-by-task-kind aggregations.
        builder.Property(x => x.TaskKind)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.PromptTokens)
            .IsRequired();

        builder.Property(x => x.CompletionTokens)
            .IsRequired();

        // EstimatedCostUsd — numeric(12,6): indicative cost; 6 dp matches gateway :F6 logging.
        // Not money type per CONVENTIONS (numeric, not money).
        builder.Property(x => x.EstimatedCostUsd)
            .IsRequired()
            .HasColumnType("numeric(12,6)");

        // LatencyMs — bigint (long).
        builder.Property(x => x.LatencyMs)
            .IsRequired();

        builder.Property(x => x.WasCacheHit)
            .IsRequired();

        // StudentId — nullable int, no cross-module FK. Always null in v1.
        builder.Property(x => x.StudentId);

        // OccurredAtUtc — timestamptz (UTC). Primary filter for P7-11 time-range queries.
        // With the legacy-timestamp AppContext switch enabled at the Host, DateTime values are
        // accepted regardless of Kind — same rationale as SafetyEvent.OccurredAtUtc.
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // Primary query pattern for P7-11: filter by time range.
        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("IX_AiUsageLogs_OccurredAtUtc");

        // Secondary index for per-model cost breakdown queries.
        builder.HasIndex(x => x.ModelId)
            .HasDatabaseName("IX_AiUsageLogs_ModelId");

        // Secondary index for per-task-kind cost breakdown queries.
        builder.HasIndex(x => x.TaskKind)
            .HasDatabaseName("IX_AiUsageLogs_TaskKind");
    }
}
