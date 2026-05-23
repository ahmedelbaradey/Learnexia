using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Skill"/>. Skill *—1 Concept (Restrict).
/// The optional Lesson—Skill relationship is configured on <see cref="LessonConfig"/>.
/// </summary>
public class SkillConfig : IEntityTypeConfiguration<Skill>
{
    public void Configure(EntityTypeBuilder<Skill> builder)
    {
        builder.ToTable("Skills", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.MasteryThreshold)
            .IsRequired();

        builder.Property(x => x.EstimatedTimeMinutes)
            .IsRequired();

        builder.HasOne(s => s.Concept)
            .WithMany(c => c.Skills)
            .HasForeignKey(s => s.ConceptId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ConceptId)
            .HasDatabaseName("IX_Skills_ConceptId");
    }
}
