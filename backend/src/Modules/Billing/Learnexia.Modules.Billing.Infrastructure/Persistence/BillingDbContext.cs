using Learnexia.Modules.Billing.Application.Abstractions;
using Learnexia.Modules.Billing.Domain.Entities;
using Learnexia.Shared.Kernel.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the Billing module. Schema = <c>"billing"</c>.
///
/// <para>Mirrors <c>AiDbContext</c>:
/// <list type="bullet">
///   <item><see cref="HasDefaultSchema"/> = <see cref="Schema"/>.</item>
///   <item><see cref="ApplyConfigurationsFromAssembly"/> for EF configs under <c>Configurations/</c>.</item>
///   <item>Audit-stamping <see cref="SaveChangesAsync(int)"/> override.</item>
///   <item><c>PendingModelChangesWarning</c> suppressed.</item>
/// </list>
/// </para>
///
/// <para>Implements <see cref="IBillingDbContext"/> so Application-layer handlers can use the
/// abstraction without referencing the Infrastructure project.</para>
/// </summary>
public class BillingDbContext : DbContext, IBillingDbContext
{
    public const string Schema = "billing";

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    public DbSet<CreditAccount> CreditAccounts => Set<CreditAccount>();
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

    protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        => optionsBuilder.ConfigureWarnings(w => w.Ignore(RelationalEventId.PendingModelChangesWarning));

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasDefaultSchema(Schema);
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(BillingDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }

    /// <summary>
    /// Stamps audit fields on tracked entities before saving.
    /// Mirrors <c>AiDbContext.SaveChangesAsync(int userId)</c>.
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
