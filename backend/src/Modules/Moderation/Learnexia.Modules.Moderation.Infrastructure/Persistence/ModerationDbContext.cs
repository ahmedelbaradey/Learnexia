using Learnexia.Modules.Moderation.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Moderation.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Moderation module. Schema = "moderation" (schema-per-module).
///
/// Mirrors <c>LearningDbContext</c>:
/// - <see cref="HasDefaultSchema"/> set to <see cref="Schema"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all <c>IEntityTypeConfiguration&lt;T&gt;</c>
///   files in Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int)"/> override stamps timestamps + user IDs.
///
/// The <c>AuditLog</c> table is append-only: no Update or Delete command, handler, or endpoint exists.
/// The API layer enforces immutability. A future DB-layer revoke (P7-12 Q3) can be added in migration.
///
/// Option C (2026-06-16): the Application layer no longer references this DbContext directly.
/// All EF access is mediated by <c>AuditLogQueryService</c> and <c>AuditLogWriter</c>.
/// </summary>
public class ModerationDbContext : DbContext
{
    public const string Schema = "moderation";

    public ModerationDbContext(DbContextOptions<ModerationDbContext> options) : base(options)
    {
    }

    public DbSet<AuditLog> AuditLogs => Set<AuditLog>();
    public DbSet<ModerationItem> ModerationItems => Set<ModerationItem>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(ModerationDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors LearningDbContext.SaveChangesAsync(userId).
    /// Called by the per-module <c>UnitOfWorkBehavior</c> for write commands, or directly by event handlers.
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
