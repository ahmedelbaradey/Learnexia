using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentSkillMastery"/>.
///
/// Key decisions:
/// - <c>Status</c> stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>; column default 0 (NotStarted).
/// - <c>StudentId</c> is a plain <c>int</c> (no FK to Identity — module isolation rule 1); indexed.
/// - <c>SkillId</c> is an intra-Learning FK → <c>Skill.Id</c> (Restrict — cannot delete a skill with
///   mastery rows). Both entities live in the <c>learning</c> schema.
/// - Unique composite index on <c>(StudentId, SkillId)</c> — one mastery row per student/skill.
/// - Secondary index on <c>StudentId</c> — covers the "GET /Mastery" (all rows for one student) query.
/// - UTC columns (<c>LastPracticedAt</c>, <c>NextReviewDueAt</c>) use <c>timestamptz</c> (PostgreSQL
///   timestamp with time zone) via <c>HasColumnType("timestamptz")</c>.
/// - Spaced-repetition columns (<c>ReviewIntervalDays</c>, <c>RepetitionNumber</c>) default to 0;
///   <c>NextReviewDueAt</c> is nullable (no default needed).
/// </summary>
public class StudentSkillMasteryConfig : IEntityTypeConfiguration<StudentSkillMastery>
{
    public void Configure(EntityTypeBuilder<StudentSkillMastery> builder)
    {
        builder.ToTable("StudentSkillMasteries", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // ── Loose reference (module isolation — no cross-module FK) ───────────────────────────────
        builder.Property(x => x.StudentId)
            .IsRequired();

        // ── Intra-Learning FK to Skill (Restrict) ─────────────────────────────────────────────────
        builder.Property(x => x.SkillId)
            .IsRequired();

        builder.HasOne(x => x.Skill)
            .WithMany()
            .HasForeignKey(x => x.SkillId)
            .OnDelete(DeleteBehavior.Restrict);

        // ── Mastery fields ────────────────────────────────────────────────────────────────────────
        builder.Property(x => x.MasteryPercentage)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(MasteryStatus.NotStarted);

        builder.Property(x => x.AttemptsCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.LastPracticedAt)
            .IsRequired()
            .HasColumnType("timestamptz");

        // ── Spaced-repetition columns (P3-10 reservation) ─────────────────────────────────────────
        builder.Property(x => x.ReviewIntervalDays)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.NextReviewDueAt)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        builder.Property(x => x.RepetitionNumber)
            .IsRequired()
            .HasDefaultValue(0);

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────
        // Unique composite — one mastery row per (student, skill).
        builder.HasIndex(x => new { x.StudentId, x.SkillId })
            .IsUnique()
            .HasDatabaseName("UX_StudentSkillMasteries_StudentId_SkillId");

        // Secondary — covers the "all mastery for one student" read path (GET /api/Learning/Mastery).
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_StudentSkillMasteries_StudentId");
    }
}
