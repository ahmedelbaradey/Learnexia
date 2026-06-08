using Learnexia.Modules.Learning.Domain.Entities;
using Learnexia.Modules.Learning.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for QuizQuestion.
/// - QuestionType, Difficulty, GeneratedBy stored as int via HasConversion.
/// - Options, CorrectAnswer stored as jsonb columns for queryability.
/// - LessonId, SkillId are plain int — index only, no FK (decouples quiz lifecycle from lesson deletes).
///
/// P7-04: Added <c>SequenceOrder</c> (int, DEFAULT 0) for ordering questions within a lesson's
/// implicit quiz set, <c>IsActive</c> (bool, DEFAULT true) for admin-controlled visibility toggle,
/// and a composite index <c>(LessonId, SequenceOrder)</c> for ordered question reads.
/// The global soft-delete filter (<c>IsDeleted != true</c>) is registered on QuizQuestion in
/// <see cref="LearningDbContext.OnModelCreating"/> — see that class for the required-relationship
/// filter rationale (LessonId is a loose int, so no filter-chain warning from EF Core).
/// IsActive is intentionally NOT in the global filter — admins must see inactive questions to
/// re-activate them; student-facing reads apply IsActive == true per-query.
/// </summary>
public class QuizQuestionConfig : IEntityTypeConfiguration<QuizQuestion>
{
    public void Configure(EntityTypeBuilder<QuizQuestion> builder)
    {
        builder.ToTable("QuizQuestions", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.LessonId)
            .IsRequired();

        builder.Property(x => x.SkillId)
            .IsRequired(false);

        builder.Property(x => x.QuestionType)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.QuestionText)
            .IsRequired();

        builder.Property(x => x.Options)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.CorrectAnswer)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.Difficulty)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.GeneratedBy)
            .IsRequired()
            .HasConversion<int>();

        // P7-04: SequenceOrder — display order within the owning lesson's question set.
        // DEFAULT 0 so existing/seeded rows backfill cleanly without a data migration.
        builder.Property(x => x.SequenceOrder)
            .IsRequired()
            .HasDefaultValue(0);

        // P7-04: IsActive — admin-controlled visible/hidden toggle (distinct from soft-delete).
        // DEFAULT true so existing/seeded rows remain visible without a data migration.
        builder.Property(x => x.IsActive)
            .IsRequired()
            .HasDefaultValue(true);

        // P7-05: LifecycleState — editorial lifecycle. DB DEFAULT 2 (Published) backfills existing rows.
        // C# initializer Draft ensures new inserts default to Draft at the application layer.
        builder.Property(x => x.LifecycleState)
            .IsRequired()
            .HasConversion<int>()
            .HasDefaultValue(LifecycleState.Published);

        builder.HasIndex(x => x.LessonId)
            .HasDatabaseName("IX_QuizQuestions_LessonId");

        builder.HasIndex(x => x.SkillId)
            .HasDatabaseName("IX_QuizQuestions_SkillId");

        // P7-04: Composite index for ordered reads of a lesson's question set.
        // Mirrors IX_ContentBlocks_LessonId_SequenceOrder and IX_Lessons_UnitId_SequenceOrder.
        builder.HasIndex(x => new { x.LessonId, x.SequenceOrder })
            .HasDatabaseName("IX_QuizQuestions_LessonId_SequenceOrder");
    }
}
