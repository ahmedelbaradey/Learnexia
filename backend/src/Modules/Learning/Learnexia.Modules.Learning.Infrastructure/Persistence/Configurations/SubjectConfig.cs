using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Subject"/>. Subject *—1 Grade (Restrict);
/// Subject 1—* Unit and Subject 1—* Concept (configured on the child side).
/// </summary>
public class SubjectConfig : IEntityTypeConfiguration<Subject>
{
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        builder.ToTable("Subjects", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Name)
            .IsRequired();

        // Country nullable (single-country MVP) — explicit for clarity.
        builder.Property(x => x.Country)
            .IsRequired(false);

        builder.HasIndex(x => x.GradeId)
            .HasDatabaseName("IX_Subjects_GradeId");
    }
}
