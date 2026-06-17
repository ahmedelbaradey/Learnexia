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
/// <para>Implements <see cref="IBillingDbContext"/> (declared in the same namespace) so
/// Infrastructure-layer services can use the abstraction for testing seams.</para>
/// </summary>
public class BillingDbContext : DbContext, IBillingDbContext
{
    public const string Schema = "billing";

    public BillingDbContext(DbContextOptions<BillingDbContext> options) : base(options) { }

    // CreditAccounts DbSet removed — CreditAccount entity retired, table dropped in
    // DropLegacyCreditAccounts migration. CreditAccountMigrationService uses raw SQL.
    public DbSet<CreditTransaction> CreditTransactions => Set<CreditTransaction>();
    public DbSet<GlobalSetting> GlobalSettings => Set<GlobalSetting>();

    // P10-05 subscription plan entities.
    public DbSet<Plan> Plans => Set<Plan>();
    public DbSet<Subscription> Subscriptions => Set<Subscription>();

    // P10-06 payment entities.
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<WebhookEvent> WebhookEvents => Set<WebhookEvent>();

    // P10-13 family energy wallet entities.
    public DbSet<FamilyEnergyAccount> FamilyEnergyAccounts => Set<FamilyEnergyAccount>();
    public DbSet<ChildEnergyAllocation> ChildEnergyAllocations => Set<ChildEnergyAllocation>();
    public DbSet<ChildDailyUsage> ChildDailyUsages => Set<ChildDailyUsage>();

    // P10-14 seat reservation entity.
    public DbSet<SeatReservation> SeatReservations => Set<SeatReservation>();

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
