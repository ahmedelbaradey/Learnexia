using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for StudentAnswer.
/// - AttemptId: real intra-module FK to Attempt with Cascade delete (deleting an Attempt removes its answers).
/// - QuestionId: real intra-module FK to QuizQuestion with Restrict (cannot delete a question while answers exist).
/// - Indexed: AttemptId, QuestionId.
/// </summary>
public class StudentAnswerConfig : IEntityTypeConfiguration<StudentAnswer>
{
    public void Configure(EntityTypeBuilder<StudentAnswer> builder)
    {
        builder.ToTable("StudentAnswers", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AnswerPayload)
            .IsRequired();

        builder.Property(x => x.IsCorrect)
            .IsRequired();

        builder.Property(x => x.TimeSpentSeconds)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.HintUsed)
            .IsRequired()
            .HasDefaultValue(false);

        builder.HasOne(sa => sa.Attempt)
            .WithMany(a => a.StudentAnswers)
            .HasForeignKey(sa => sa.AttemptId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne(sa => sa.Question)
            .WithMany(q => q.StudentAnswers)
            .HasForeignKey(sa => sa.QuestionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.AttemptId)
            .HasDatabaseName("IX_StudentAnswers_AttemptId");

        builder.HasIndex(x => x.QuestionId)
            .HasDatabaseName("IX_StudentAnswers_QuestionId");
    }
}
