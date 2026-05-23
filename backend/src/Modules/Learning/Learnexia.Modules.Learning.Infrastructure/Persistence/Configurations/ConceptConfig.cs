using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Concept"/>. Concept *—1 Subject (Restrict); Concept 1—* Skill.
/// <c>DifficultyLevel</c> stored as int.
/// </summary>
public class ConceptConfig : IEntityTypeConfiguration<Concept>
{
    public void Configure(EntityTypeBuilder<Concept> builder)
    {
        builder.ToTable("Concepts", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        builder.Property(x => x.Description)
            .IsRequired(false);

        // Enum stored as int (no free-text rule).
        builder.Property(x => x.DifficultyLevel)
            .HasConversion<int>()
            .IsRequired();

        builder.HasOne(c => c.Subject)
            .WithMany(s => s.Concepts)
            .HasForeignKey(c => c.SubjectId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SubjectId)
            .HasDatabaseName("IX_Concepts_SubjectId");
    }
}
