using Learnexia.Modules.Ai.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Ai.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Ai module. Schema = "ai" (schema-per-module).
///
/// Mirrors <c>ModerationDbContext</c>:
/// - <see cref="HasDefaultSchema"/> set to <see cref="Schema"/>.
/// - <see cref="ApplyConfigurationsFromAssembly"/> auto-discovers all <c>IEntityTypeConfiguration&lt;T&gt;</c>
///   files in Configurations/.
/// - Audit-stamping <see cref="SaveChangesAsync(int)"/> override stamps timestamps + user IDs.
///
/// <para>The <c>SafetyEvents</c> table is append-only: no Update or Delete command, handler,
/// or endpoint exists. The application layer enforces immutability.</para>
///
/// <para>Write path: <c>SafetyLayer.GenerateSafeAsync</c> calls <c>AiDbContext.SaveChangesAsync(userId)</c>
/// directly (not via UoW behavior — this is an append-only event log, same pattern as Moderation).
/// Per ADR-0001: direct SaveChangesAsync is correct for append-only logs outside the command pipeline.</para>
/// </summary>
public class AiDbContext : DbContext
{
    public const string Schema = "ai";

    public AiDbContext(DbContextOptions<AiDbContext> options) : base(options)
    {
    }

    public DbSet<SafetyEvent> SafetyEvents => Set<SafetyEvent>();
    public DbSet<AiResponseCache> AiResponseCaches => Set<AiResponseCache>();
    public DbSet<AiUsageLog> AiUsageLogs => Set<AiUsageLog>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AiDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving. Mirrors ModerationDbContext.SaveChangesAsync(userId).
    /// Called directly by <c>SafetyLayer</c> when persisting a <c>SafetyEvent</c>.
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
