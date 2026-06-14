using Learnexia.Modules.Ai.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Ai.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="SafetyEvent"/>.
///
/// Key decisions:
/// - <see cref="SafetyEvent.StudentId"/> is a plain <c>int</c> — no cross-module FK (module isolation rule 1).
/// - <see cref="SafetyEvent.FailedChecks"/> and <see cref="SafetyEvent.ReasonCodes"/> use column type
///   <c>jsonb</c> — stored as serialized JSON arrays. Application layer owns the shape.
///   Same pattern as <c>QuizQuestion.Options</c> and <c>StudentLearningProfile.QuestionTypeAffinity</c>.
/// - <see cref="SafetyEvent.OccurredAtUtc"/> uses column type <c>timestamptz</c> (UTC) and is indexed
///   for P7-09/P7-11 time-range queries. Same pattern as <c>AuditLog.OccurredAtUtc</c>.
/// - No FK constraints: append-only, PII-light by design (P3-02 Q5/Q6).
/// </summary>
public class SafetyEventConfig : IEntityTypeConfiguration<SafetyEvent>
{
    public void Configure(EntityTypeBuilder<SafetyEvent> builder)
    {
        builder.ToTable("SafetyEvents");

        builder.HasKey(x => x.Id);

        // StudentId — plain int, no cross-module FK.
        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.TaskKind)
            .IsRequired()
            .HasMaxLength(100);

        // FailedChecks — JSON array of check names that failed.
        // Stored as jsonb for queryability. Application layer owns the JSON shape.
        // Default "[]" = no checks failed (should only be written when checks fail, but defensive default).
        builder.Property(x => x.FailedChecks)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'");

        // ReasonCodes — JSON array of all collected reason codes across all checks.
        // Stored as jsonb for queryability (P7-09 categorizes by these stable codes).
        builder.Property(x => x.ReasonCodes)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'");

        builder.Property(x => x.ActionTaken)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ModelId)
            .IsRequired()
            .HasMaxLength(200);

        // OccurredAtUtc — timestamptz (UTC). Indexed for time-range filtering by P7-09/P7-11.
        // With the legacy-timestamp AppContext switch enabled at the Host, DateTime values are
        // accepted regardless of Kind — same rationale as AuditLog.OccurredAtUtc.
        builder.Property(x => x.OccurredAtUtc)
            .HasColumnType("timestamptz")
            .IsRequired();

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // Primary query pattern for P7-09 and P7-11: filter by time range.
        builder.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("IX_SafetyEvents_OccurredAtUtc");

        // Secondary index for per-student safety history queries.
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_SafetyEvents_StudentId");

        // Secondary index for filtering by action taken (monitoring dashboard).
        builder.HasIndex(x => x.ActionTaken)
            .HasDatabaseName("IX_SafetyEvents_ActionTaken");
    }
}
