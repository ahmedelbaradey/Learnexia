using Learnexia.Modules.Identity.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Identity.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for the <see cref="ParentStudent"/> join entity.
/// Auto-discovered via <c>ApplyConfigurationsFromAssembly</c> in <c>IdentityModuleDbContext.OnModelCreating</c>.
/// </summary>
public class ParentStudentConfig : IEntityTypeConfiguration<ParentStudent>
{
    public void Configure(EntityTypeBuilder<ParentStudent> builder)
    {
        builder.ToTable("ParentStudents", IdentityModuleDbContext.Schema);

        // Composite primary key — also acts as the uniqueness constraint for AC-4 / AC-6.
        builder.HasKey(x => new { x.ParentId, x.StudentId });

        // FK: ParentId → AspNetUsers.Id
        // DeleteBehavior.Restrict: deleting a User who is linked as a parent must be handled
        // explicitly (unlink first). Avoids cascading deletes and the multiple-cascade-path
        // issue Postgres would raise if both sides used Cascade.
        builder
            .HasOne(ps => ps.Parent)
            .WithMany(u => u.LinksAsParent)
            .HasForeignKey(ps => ps.ParentId)
            .OnDelete(DeleteBehavior.Restrict);

        // FK: StudentId → AspNetUsers.Id
        // Same Restrict policy — consistent with the global Restrict default set in OnModelCreating.
        builder
            .HasOne(ps => ps.Student)
            .WithMany(u => u.LinksAsStudent)
            .HasForeignKey(ps => ps.StudentId)
            .OnDelete(DeleteBehavior.Restrict);

        // Index on StudentId for the reverse lookup "who are this student's parents?"
        builder
            .HasIndex(ps => ps.StudentId)
            .HasDatabaseName("IX_ParentStudents_StudentId");

        builder.Property(ps => ps.CreatedAt)
            .IsRequired()
            .HasDefaultValueSql("now()")
            .HasComment("UTC timestamp when the parent-student link was created");

        builder.Property(ps => ps.CreatedBy)
            .HasComment("Id of the user who created the link (plain int, no FK constraint)");
    }
}
