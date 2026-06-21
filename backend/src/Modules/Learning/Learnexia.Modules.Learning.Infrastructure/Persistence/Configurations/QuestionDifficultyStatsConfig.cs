using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="QuestionDifficultyStats"/> (P5-07 BE-6).
///
/// Key decisions:
/// - <c>QuestionId</c> is a real intra-module FK to <c>QuizQuestion</c> (DeleteBehavior.Restrict) —
///   consistent with <c>StudentAnswer.QuestionId</c> (same table, same behaviour: cannot delete a
///   question while stats exist). OQ-2 resolved: real FK.
/// - <c>ComputedAtUtc</c> uses <c>timestamptz</c> — same pattern as
///   <c>StudentSkillMastery.LastPracticedAt</c> and <c>StudentRecommendation.GeneratedAtUtc</c>.
/// - Unique index on <c>QuestionId</c> (<c>UX_QuestionDifficultyStats_QuestionId</c>) enforces one
///   stats row per question — the upsert idempotency key (mirrors
///   <c>UX_StudentRecommendations_StudentId_RecommendationDate</c>).
/// - All numeric aggregate columns (<c>PValue</c>, <c>AvgTimeSpentSeconds</c>,
///   <c>HintUsageRate</c>) are <c>double precision</c> — no explicit HasColumnType needed
///   (EF/Npgsql maps <c>double</c> to <c>double precision</c> by convention).
/// - No soft-delete query filter on this entity — it is a system aggregate table, not user content.
///   The <c>IsDeleted</c> audit column is inherited from <c>FullAuditedEntity</c> and preserved.
/// - Discovered automatically by <c>LearningDbContext.ApplyConfigurationsFromAssembly</c>.
/// </summary>
public class QuestionDifficultyStatsConfig : IEntityTypeConfiguration<QuestionDifficultyStats>
{
    public void Configure(EntityTypeBuilder<QuestionDifficultyStats> builder)
    {
        builder.ToTable("QuestionDifficultyStats", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // ── QuestionId — real intra-module FK (Restrict, mirrors StudentAnswer.QuestionId) ──────────
        builder.Property(x => x.QuestionId)
            .IsRequired();

        builder.HasOne(x => x.Question)
            .WithMany()
            .HasForeignKey(x => x.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Aggregate columns ─────────────────────────────────────────────────────────────────────
        builder.Property(x => x.SampleSize)
            .IsRequired();

        builder.Property(x => x.PValue)
            .IsRequired();

        builder.Property(x => x.AvgTimeSpentSeconds)
            .IsRequired();

        builder.Property(x => x.HintUsageRate)
            .IsRequired();

        // ── ComputedAtUtc — timestamptz (UTC) — mirrors StudentRecommendation.GeneratedAtUtc ──────
        builder.Property(x => x.ComputedAtUtc)
            .IsRequired()
            .HasColumnType("timestamptz");

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // Unique — one stats row per question (upsert idempotency key for CalibrationJob).
        // Mirrors UX_StudentRecommendations_StudentId_RecommendationDate.
        builder.HasIndex(x => x.QuestionId)
            .IsUnique()
            .HasDatabaseName("UX_QuestionDifficultyStats_QuestionId");
    }
}
