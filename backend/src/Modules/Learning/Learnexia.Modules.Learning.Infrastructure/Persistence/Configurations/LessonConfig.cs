using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Lesson"/>. Lesson *—1 Unit (Restrict) and
/// Lesson *—o1 Skill (optional; SetNull on delete). <c>Difficulty</c> stored as int.
/// </summary>
public class LessonConfig : IEntityTypeConfiguration<Lesson>
{
    public void Configure(EntityTypeBuilder<Lesson> builder)
    {
        builder.ToTable("Lessons", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.SequenceOrder)
            .IsRequired();

        builder.Property(x => x.IsLocked)
            .IsRequired();

        // Enum stored as int (no free-text rule).
        builder.Property(x => x.Difficulty)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(l => l.Unit)
            .WithMany(u => u.Lessons)
            .HasForeignKey(l => l.UnitId)
            .OnDelete(DeleteBehavior.Restrict);

        // Optional "Lesson teaches Skill" — nullable FK, SetNull on delete (lead decision).
        builder.HasOne(l => l.Skill)
            .WithMany(s => s.Lessons)
            .HasForeignKey(l => l.SkillId)
            .IsRequired(false)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.UnitId)
            .HasDatabaseName("IX_Lessons_UnitId");

        builder.HasIndex(x => x.SkillId)
            .HasDatabaseName("IX_Lessons_SkillId");

        // Composite index for ordered reads within a unit.
        builder.HasIndex(x => new { x.UnitId, x.SequenceOrder })
            .HasDatabaseName("IX_Lessons_UnitId_SequenceOrder");
    }
}
