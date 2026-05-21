using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Catalog.Infrastructure.Persistence;

// Design-time factory for EF tooling (migrations). Not used at runtime — the Host registers the
// context via DI. Reads ConnectionStrings:Default from the startup project's appsettings, so run with
// --startup-project src/Host/Learnexia.Host (its directory becomes the config base path).
public class CatalogDbContextFactory : IDesignTimeDbContextFactory<CatalogDbContext>
{
    public CatalogDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<CatalogDbContext>()
            .UseNpgsql(connectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema)
                .MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName)
                .UseVector())
            .Options;

        return new CatalogDbContext(options);
    }
}
