using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Billing.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF tooling (migrations). Mirrors <c>AiDbContextFactory</c>.
/// Run EF commands with <c>--startup-project src/Host/Learnexia.Host</c>.
/// </summary>
public class BillingDbContextFactory : IDesignTimeDbContextFactory<BillingDbContext>
{
    public BillingDbContext CreateDbContext(string[] args)
    {
        var configuration = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true)
            .AddJsonFile($"appsettings.{Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Development"}.json", optional: true)
            .AddEnvironmentVariables()
            .Build();

        var connectionString = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException(
                "ConnectionStrings:Default not found. Run EF with --startup-project src/Host/Learnexia.Host.");

        var options = new DbContextOptionsBuilder<BillingDbContext>()
            .UseNpgsql(connectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", BillingDbContext.Schema)
                .MigrationsAssembly(typeof(BillingDbContext).Assembly.FullName))
            .Options;

        return new BillingDbContext(options);
    }
}
