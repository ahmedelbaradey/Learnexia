using Learnexia.Modules.Parent.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Parent.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Parent module. Schema = "parent" (schema-per-module). Mirrors
/// LearningDbContext: <see cref="HasDefaultSchema"/>, <see cref="ApplyConfigurationsFromAssembly"/>, and
/// an audit-stamping <see cref="SaveChangesAsync(int)"/> override. Owns only the <see cref="ParentStudent"/>
/// link table; child accounts live in Identity and are reached via the IChildAccountService seam.
/// </summary>
public class ParentDbContext : DbContext
{
    public const string Schema = ParentSchema.Name;

    public ParentDbContext(DbContextOptions<ParentDbContext> options) : base(options)
    {
    }

    public DbSet<ParentStudent> ParentStudents => Set<ParentStudent>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ParentDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors LearningDbContext.SaveChangesAsync(userId).
    /// ParentStudent is not an audited base type, so this is a no-op for it today, but the override is kept
    /// for parity and for any future audited entity in this module.
    /// </summary>
    public virtual async Task<int> SaveChangesAsync(int userId)
    {
        foreach (var entry in ChangeTracker.Entries<CreationAuditedEntity>())
        {
            if (entry.State == EntityState.Added)
            {
                entry.Entity.CreatedAt = DateTime.Now;
                entry.Entity.CreatedBy = userId;
            }
        }

        foreach (var entry in ChangeTracker.Entries<AduitedEntity>())
        {
            if (entry.State == EntityState.Modified)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
                entry.Entity.UpdatedBy = userId;
            }
            else if (entry.State == EntityState.Added)
            {
                entry.Entity.UpdatedAt = DateTime.Now;
            }
        }

        foreach (var entry in ChangeTracker.Entries<FullAuditedEntity>())
        {
            if (entry.State == EntityState.Modified && entry.Entity.IsDeleted == true)
            {
                entry.Entity.DeletedAt = DateTime.Now;
                entry.Entity.DeletedBy = userId;
            }
        }

        return await base.SaveChangesAsync();
    }
}
