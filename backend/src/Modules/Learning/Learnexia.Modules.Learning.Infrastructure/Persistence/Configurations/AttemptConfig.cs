using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for Attempt.
/// - Status stored as int via HasConversion.
/// - StudentId, LessonId are plain int — index only, no FK (module isolation for StudentId; plain int for LessonId).
/// - CompletedAt is nullable.
/// </summary>
public class AttemptConfig : IEntityTypeConfiguration<Attempt>
{
    public void Configure(EntityTypeBuilder<Attempt> builder)
    {
        builder.ToTable("Attempts", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.StudentId)
            .IsRequired();

        builder.Property(x => x.LessonId)
            .IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<int>();

        builder.Property(x => x.AccuracyPercentage)
            .IsRequired()
            .HasDefaultValue(0.0);

        builder.Property(x => x.DurationSeconds)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.HintsUsedCount)
            .IsRequired()
            .HasDefaultValue(0);

        builder.Property(x => x.StartedAt)
            .IsRequired();

        builder.Property(x => x.CompletedAt)
            .IsRequired(false);

        builder.HasIndex(x => x.StudentId)
            .HasDatabaseName("IX_Attempts_StudentId");

        builder.HasIndex(x => x.LessonId)
            .HasDatabaseName("IX_Attempts_LessonId");

        // Composite index for per-student + status filter (P2-08 perf — speeds up GetStudentAttempts).
        builder.HasIndex(x => new { x.StudentId, x.Status })
            .HasDatabaseName("IX_Attempts_StudentId_Status");
    }
}
