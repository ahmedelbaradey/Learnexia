using Learnexia.Modules.Learning.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for <see cref="Grade"/> (Grade 1—* Subject; Restrict on delete).
/// Auto-discovered via ApplyConfigurationsFromAssembly in LearningDbContext.OnModelCreating.
/// </summary>
public class GradeConfig : IEntityTypeConfiguration<Grade>
{
    public void Configure(EntityTypeBuilder<Grade> builder)
    {
        builder.ToTable("Grades", LearningSchema.Name);

        builder.HasKey(x => x.Id);

        builder.Property(x => x.Number)
            .IsRequired();

        builder.Property(x => x.DisplayName)
            .IsRequired();

        builder.HasMany(g => g.Subjects)
            .WithOne(s => s.Grade)
            .HasForeignKey(s => s.GradeId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
