using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Learning.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF tooling (migrations). Not used at runtime — the Host registers the
/// context via DI. Reads ConnectionStrings:Default from the startup project's appsettings, so run with
/// --startup-project src/Host/Learnexia.Host (its directory becomes the config base path).
///
/// Mirrors CatalogDbContextFactory but without pgvector/UseVector().
/// </summary>
public class LearningDbContextFactory : IDesignTimeDbContextFactory<LearningDbContext>
{
    public LearningDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<LearningDbContext>()
            .UseNpgsql(connectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", LearningDbContext.Schema)
                .MigrationsAssembly(typeof(LearningDbContext).Assembly.FullName))
            .Options;

        return new LearningDbContext(options);
    }
}
