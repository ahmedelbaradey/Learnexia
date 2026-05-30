using Learnexia.Modules.Gamification.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Gamification.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Gamification module. Schema = "gamification" (schema-per-module).
///
/// Mirrors <c>LearningDbContext</c> shape:
/// - <see cref="HasDefaultSchema"/> set to <see cref="Schema"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all
///   <c>IEntityTypeConfiguration&lt;T&gt;</c> files in Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int)"/> override iterates CreationAuditedEntity /
///   AduitedEntity / FullAuditedEntity entries and stamps timestamps + user IDs before saving.
/// - MigrationsHistoryTable is placed in the <c>gamification</c> schema for module isolation.
///
/// Apply migrations:
///   dotnet ef database update --context GamificationDbContext
///     --project src/Modules/Gamification/Learnexia.Modules.Gamification.Infrastructure
///     --startup-project src/Host/Learnexia.Host
/// </summary>
public class GamificationDbContext : DbContext
{
    public const string Schema = "gamification";

    public GamificationDbContext(DbContextOptions<GamificationDbContext> options) : base(options)
    {
    }

    public DbSet<StudentXpProfile> StudentXpProfiles => Set<StudentXpProfile>();
    public DbSet<XpAward> XpAwards => Set<XpAward>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GamificationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors LearningDbContext.SaveChangesAsync(userId).
    /// Called by the per-module <c>UnitOfWorkBehavior</c> after the command handler stages its changes.
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
