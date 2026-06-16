using Learnexia.Modules.Billing.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence;

/// <summary>
/// Infrastructure-layer abstraction over <c>BillingDbContext</c>.
/// Consumed only within Billing.Infrastructure (by <c>CreditSpendService</c>,
/// <c>RefundService</c>, <c>ConfigDefaultSubscriptionContract</c>, and DI registration).
///
/// <para>Moved from <c>Billing.Application.Abstractions</c> during the Option C refactor —
/// Application layer must be EF-free, so EF-typed interfaces cannot live there.</para>
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
