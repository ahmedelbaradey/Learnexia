using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;

namespace Learnexia.Modules.Ai.Infrastructure.Persistence;

// Design-time factory for EF tooling (migrations). Not used at runtime — the Host registers the
// context via DI. Reads ConnectionStrings:Default from the startup project's appsettings, so run with
// --startup-project src/Host/Learnexia.Host (its directory becomes the config base path).
// Mirrors ModerationDbContextFactory.
public class AiDbContextFactory : IDesignTimeDbContextFactory<AiDbContext>
{
    public AiDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<AiDbContext>()
            .UseNpgsql(connectionString, sql => sql
                .MigrationsHistoryTable("__EFMigrationsHistory", AiDbContext.Schema)
                .MigrationsAssembly(typeof(AiDbContext).Assembly.FullName))
            .Options;

        return new AiDbContext(options);
    }
}
