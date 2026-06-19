using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Analytics.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Analytics module. Schema = "analytics" (schema-per-module).
///
/// Mirrors <c>ModerationDbContext</c>:
/// - <see cref="HasDefaultSchema"/> set to <see cref="Schema"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all <c>IEntityTypeConfiguration&lt;T&gt;</c>
///   files in Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int)"/> override stamps timestamps + user IDs.
///
/// The <c>ActivityEvent</c> table (added in BE-1) is append-only: no Update or Delete command,
/// handler, or endpoint exists.
///
/// Option C: the Application layer never references this DbContext directly.
/// All EF access is mediated through service interfaces (e.g. <c>IActivityEventStore</c>).
/// </summary>
public class AnalyticsDbContext : DbContext
{
    public const string Schema = "analytics";

    public AnalyticsDbContext(DbContextOptions<AnalyticsDbContext> options) : base(options)
    {
    }

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AnalyticsDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors ModerationDbContext.SaveChangesAsync(userId).
    /// Called directly by append-only event handlers (pass userId: 0 for system telemetry rows —
    /// mirrors AiUsageLogStore). No UoW for the Analytics module (ADR-0001 append-only writes).
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
