using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Subject"/>. Subject *—1 Grade (Restrict);
/// Subject 1—* Unit and Subject 1—* Concept (configured on the child side).
///
/// P8-02: Added <c>SubjectCode</c> and <c>Language</c> columns (both stored as int per the
/// no-free-text rule) and a UNIQUE index on <c>(GradeId, SubjectCode, Language)</c> — the
/// natural key for the parallel bilingual tree model (6 roots per grade).
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

        // P8-02: SubjectCode and Language stored as int (enum-as-int convention).
        builder.Property(x => x.SubjectCode)
            .HasConversion<int>()
            .IsRequired();

        builder.Property(x => x.Language)
            .HasConversion<int>()
            .IsRequired();

        builder.HasIndex(x => x.GradeId)
            .HasDatabaseName("IX_Subjects_GradeId");

        // P8-02: UNIQUE natural key — one tree per (GradeId, SubjectCode, Language).
        builder.HasIndex(x => new { x.GradeId, x.SubjectCode, x.Language })
            .IsUnique()
            .HasDatabaseName("IX_Subjects_GradeId_SubjectCode_Language");
    }
}
