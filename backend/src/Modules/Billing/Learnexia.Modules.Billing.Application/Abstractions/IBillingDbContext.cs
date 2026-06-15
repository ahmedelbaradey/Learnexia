using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Learnexia.Modules.Billing.Application.Abstractions;

/// <summary>
/// Application-layer abstraction over <c>BillingDbContext</c>.
/// Lets command/query handlers use the DbContext without depending on the Infrastructure project
/// (module isolation inward-dependency rule).
///
/// <para>Registered in DI by <c>AddBillingInfrastructure</c> as the concrete <c>BillingDbContext</c>
/// (which implements this interface). The <c>BillingDbContext</c> is also registered separately
/// for EF tooling (migrations, design-time factory).</para>
/// </summary>
public interface IBillingDbContext
{
    DbSet<CreditAccount> CreditAccounts { get; }
    DbSet<CreditTransaction> CreditTransactions { get; }
    DbSet<GlobalSetting> GlobalSettings { get; }

    // P10-05 subscription plan entities.
    DbSet<Plan> Plans { get; }
    DbSet<Subscription> Subscriptions { get; }

    // P10-06 payment entities.
    DbSet<Payment> Payments { get; }
    DbSet<WebhookEvent> WebhookEvents { get; }

    DatabaseFacade Database { get; }
    ChangeTracker ChangeTracker { get; }

    Task<int> SaveChangesAsync(int userId);
}
