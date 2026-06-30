using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentLearningProfile"/> (P3-13).
///
/// Key decisions:
/// - <c>StudentId</c> is a plain <c>int</c> (NO cross-module FK to Identity — module isolation
///   rule 1). Unique index enforces one profile row per student.
/// - <c>PreferredExplanationStyle</c> stored as <c>int</c> via <c>HasConversion&lt;int&gt;()</c>;
///   column default 0 (<c>ExplanationStyle.Standard</c>) — safe cold-start value.
/// - <c>QuestionTypeAffinity</c>, <c>RecurringErrorClusters</c>, and <c>FatigueSignal</c>
///   stored as <c>jsonb</c> columns — shape validated in the application layer by
///   <c>StudentProfileEngine</c>, not in the DB. Same pattern as <c>ContentBlock.Payload</c>
///   and <c>QuizQuestion.Options</c>: a <c>string</c> property in the entity, <c>jsonb</c>
///   column type in Postgres, serialised/deserialised by the application layer.
/// - <c>LastRecomputedAt</c> uses <c>timestamptz</c> (UTC timestamp with time zone) — same
///   pattern as <c>StudentSkillMastery.LastPracticedAt</c>.
/// - No FK to Gamification entities — <c>StudentId</c> is a loose int (behavioral attributes
///   only; no XP/streak/hearts).
/// - Discovered automatically by <c>LearningDbContext.ApplyConfigurationsFromAssembly</c>.
/// </summary>
public class StudentLearningProfileConfig : IEntityTypeConfiguration<StudentLearningProfile>
{
    public void Configure(EntityTypeBuilder<StudentLearningProfile> builder)
    {
        builder.ToTable("StudentLearningProfiles", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // ── Loose reference (module isolation — NO cross-module FK) ───────────────────────────────
        // StudentId is a plain int. The unique index below ensures one profile per student, but
        // there is no FK constraint pointing at Identity.AspNetUsers or Gamification entities.
        builder.Property(x => x.StudentId)
            .IsRequired();

        // ── Behavioral / derived attribute columns ────────────────────────────────────────────────

        // QuestionTypeAffinity — per-QuestionType success-rate map.
        // Nullable: null = cold-start (no data yet).
        builder.Property(x => x.QuestionTypeAffinity)
            .IsRequired(false)
            .HasColumnType("jsonb");

        // RecurringErrorClusters — list of skill IDs with repeated-wrong-answer patterns.
        // Nullable: null = no recurring errors detected.
        builder.Property(x => x.RecurringErrorClusters)
            .IsRequired(false)
            .HasColumnType("jsonb");

        // AttentionSpanMinutes — int? (nullable int column). No special column type needed;
        // standard Postgres integer, nullable.
        builder.Property(x => x.AttentionSpanMinutes)
            .IsRequired(false);

        // FatigueSignal — bucket-level accuracy-drop detail underlying AttentionSpanMinutes.
        // Nullable jsonb: null = no fatigue signal data yet.
        builder.Property(x => x.FatigueSignal)
            .IsRequired(false)
            .HasColumnType("jsonb");

        // PreferredExplanationStyle stored as int.
        // EF column default 0 → ExplanationStyle.Standard (safe cold-start value).
        builder.Property(x => x.PreferredExplanationStyle)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(ExplanationStyle.Standard);

        // DataPointCount — confidence indicator for cold-start detection.
        builder.Property(x => x.DataPointCount)
            .IsRequired()
            .HasDefaultValue(0);

        // LastRecomputedAt — UTC timestamp; nullable (null = never recomputed).
        // Stored as timestamptz — same pattern as StudentSkillMastery.LastPracticedAt / NextReviewDueAt.
        builder.Property(x => x.LastRecomputedAt)
            .IsRequired(false)
            .HasColumnType("timestamptz");

        // ── P3-13a new behavioral dimension columns ───────────────────────────────────────────────

        // GritScore — persistence / grit proxy. Nullable float8; null = cold-start / insufficient data.
        builder.Property(x => x.GritScore)
            .IsRequired(false)
            .HasColumnType("double precision");

        // MasteryVelocity — rate-of-improvement slope. Nullable float8; null = cold-start / insufficient data.
        builder.Property(x => x.MasteryVelocity)
            .IsRequired(false)
            .HasColumnType("double precision");

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────
        // Unique — exactly one behavioral profile per student.
        // StudentId is a loose int (no FK), but the uniqueness constraint is still enforced at the DB level.
        builder.HasIndex(x => x.StudentId)
            .IsUnique()
            .HasDatabaseName("UX_StudentLearningProfiles_StudentId");
    }
}
