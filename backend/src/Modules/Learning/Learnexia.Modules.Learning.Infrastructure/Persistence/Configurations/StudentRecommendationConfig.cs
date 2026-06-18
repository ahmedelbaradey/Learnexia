using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="StudentRecommendation"/> (P5-09).
///
/// Key decisions:
/// - <c>StudentId</c> is a plain <c>int</c> (NO cross-module FK to Identity — module isolation
///   rule 1). Composite unique index on <c>(StudentId, RecommendationDate)</c> enforces
///   idempotent daily upsert (exactly one recommendation set per student per day).
/// - <c>RecommendationDate</c> mapped as <c>date</c> (PostgreSQL date, no time component) to
///   make the day-keying unambiguous regardless of the job's run time-of-day.
/// - <c>ItemsJson</c> stored as <c>jsonb</c> — same pattern as
///   <c>StudentLearningProfile.QuestionTypeAffinity</c> and <c>ContentBlock.Payload</c>:
///   a <c>string</c> property in the entity, <c>jsonb</c> column in PostgreSQL, serialised
///   and deserialised by the application layer. Column default is <c>'[]'</c>.
/// - <c>GeneratedAtUtc</c> uses <c>timestamptz</c> — same pattern as
///   <c>StudentSkillMastery.LastPracticedAt</c> and <c>StudentLearningProfile.LastRecomputedAt</c>.
/// - No FK to any other module or any gamification entity. Grade-transition preservation:
///   historical rows survive a grade change (product rule).
/// - Discovered automatically by <c>LearningDbContext.ApplyConfigurationsFromAssembly</c>.
/// </summary>
public class StudentRecommendationConfig : IEntityTypeConfiguration<StudentRecommendation>
{
    public void Configure(EntityTypeBuilder<StudentRecommendation> builder)
    {
        builder.ToTable("StudentRecommendations", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        // ── Loose reference (module isolation — NO cross-module FK) ───────────────────────────────
        // StudentId is a plain int. No FK constraint to Identity.AspNetUsers.
        builder.Property(x => x.StudentId)
            .IsRequired();

        // ── RecommendationDate — calendar day, stored as PostgreSQL date ──────────────────────────
        // DateOnly maps to PostgreSQL `date` with Npgsql — no explicit HasColumnType needed for
        // .NET 10 + Npgsql 9, but we set it explicitly for clarity and to match the project style.
        builder.Property(x => x.RecommendationDate)
            .IsRequired()
            .HasColumnType("date");

        // ── ItemsJson — serialised RecommendationItem[] as jsonb ─────────────────────────────────
        // Same pattern as StudentLearningProfile.QuestionTypeAffinity / ContentBlock.Payload:
        // a string property in the entity, jsonb column in PostgreSQL, serialised by the app layer.
        // Default '[]' ensures cold-start rows are always valid JSON and never null.
        builder.Property(x => x.ItemsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValueSql("'[]'");

        // ── GeneratedAtUtc — timestamptz (UTC) ───────────────────────────────────────────────────
        // Mirrors StudentLearningProfile.LastRecomputedAt and StudentSkillMastery.LastPracticedAt.
        builder.Property(x => x.GeneratedAtUtc)
            .IsRequired()
            .HasColumnType("timestamptz");

        // ── Indexes ───────────────────────────────────────────────────────────────────────────────

        // Composite unique — one recommendation set per (student, day).
        // This is the idempotency key for the daily upsert: the job can run multiple times
        // in a day and only the latest write wins (UPDATE on conflict, not duplicate INSERT).
        builder.HasIndex(x => new { x.StudentId, x.RecommendationDate })
            .IsUnique()
            .HasDatabaseName("UX_StudentRecommendations_StudentId_RecommendationDate");

        // Single-column index on StudentId — covers the "latest recommendation for one student"
        // read path used by IStudentRecommendationsQuery (P5-09-BE-5).
        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_StudentRecommendations_StudentId");
    }
}
