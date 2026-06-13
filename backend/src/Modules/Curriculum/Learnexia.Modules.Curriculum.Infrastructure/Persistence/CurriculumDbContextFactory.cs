using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.Extensions.Configuration;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Curriculum.Infrastructure.Persistence;

/// <summary>
/// Design-time factory for EF tooling (migrations). Not used at runtime — the Host registers the
/// context via DI. Reads ConnectionStrings:Default from the startup project's appsettings, so run with
/// --startup-project src/Host/Learnexia.Host (its directory becomes the config base path).
///
/// Mirrors AiDbContextFactory / ModerationDbContextFactory.
///
/// pgvector: UseVector() enables the pgvector EF Core plugin for the design-time context so that
/// migrations can translate the Vector property to vector(1024) and emit HasPostgresExtension("vector").
/// </summary>
public class CurriculumDbContextFactory : IDesignTimeDbContextFactory<CurriculumDbContext>
{
    public CurriculumDbContext CreateDbContext(string[] args)
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

        var options = new DbContextOptionsBuilder<CurriculumDbContext>()
            .UseNpgsql(connectionString, sql => sql
                .UseVector()
                .MigrationsHistoryTable("__EFMigrationsHistory", CurriculumDbContext.Schema)
                .MigrationsAssembly(typeof(CurriculumDbContext).Assembly.FullName))
            .Options;

        return new CurriculumDbContext(options);
    }
}
