using Learnexia.Modules.Parent.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Parent.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ParentStudent"/> join entity. Auto-discovered via
/// <c>ApplyConfigurationsFromAssembly</c>. Both ParentId and StudentId are PLAIN ints with NO foreign-key
/// constraint to the Identity User table (module isolation, rule 1; no cross-module FK).
/// </summary>
public class ParentStudentConfig : IEntityTypeConfiguration<ParentStudent>
{
    public void Configure(EntityTypeBuilder<ParentStudent> builder)
    {
        builder.ToTable("ParentStudent", ParentDbContext.Schema);

        // Composite primary key — also the uniqueness constraint for a (parent, student) link.
        builder.HasKey(x => new { x.ParentId, x.StudentId });

        // Reverse-lookup index: "who are this student's parents?" (used by the last-parent guard).
        builder
            .HasIndex(ps => ps.StudentId)
            .HasDatabaseName("IX_ParentStudent_StudentId");

        builder.Property(ps => ps.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasComment("UTC timestamp when the parent-student link was created");

        builder.Property(ps => ps.CreatedBy)
            .HasComment("Id of the user who created the link (plain int, no FK constraint)");
    }
}
