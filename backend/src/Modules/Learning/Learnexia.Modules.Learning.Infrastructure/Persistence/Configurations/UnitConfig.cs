using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Unit"/>. Unit *—1 Subject (Restrict); Unit 1—* Lesson.
/// </summary>
public class UnitConfig : IEntityTypeConfiguration<Unit>
{
    public void Configure(EntityTypeBuilder<Unit> builder)
    {
        builder.ToTable("Units", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.SequenceOrder)
            .IsRequired();

        builder.HasOne(u => u.Subject)
            .WithMany(s => s.Units)
            .HasForeignKey(u => u.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("IX_Units_SubjectId");

        // Composite index for ordered reads within a subject.
        builder.HasIndex(x => new { x.SubjectId, x.SequenceOrder })
            .HasDatabaseName("IX_Units_SubjectId_SequenceOrder");
    }
}
