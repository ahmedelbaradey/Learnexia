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

        builder.HasIndex(x => x.LessonId)
            .HasDatabaseName("IX_QuizQuestions_LessonId");

        builder.HasIndex(x => x.SkillId)
            .HasDatabaseName("IX_QuizQuestions_SkillId");
    }
}
