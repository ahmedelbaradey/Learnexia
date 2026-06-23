using Learnexia.Modules.Curriculum.Application;
using Learnexia.Modules.Curriculum.Infrastructure;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence;
using Learnexia.Modules.Curriculum.Infrastructure.Persistence.Seed;
using Learnexia.Modules.Curriculum.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace Learnexia.Modules.Curriculum.Api;

/// <summary>
/// Module entry point for the Curriculum module. Called from the Host <c>Program.cs</c> via
/// <c>builder.Services.AddCurriculumModule(builder.Configuration)</c>.
///
/// <para>Wires Application (AutoMapper + validators + ValidationBehavior) + Infrastructure
/// (DbContext + IEmbeddingProvider typed HttpClient + seeder + RagContextProvider).</para>
///
/// <para>The dev-only <c>RetrievalController</c> (BE-8) is included via
/// <c>AddApplicationPart</c> — it is locked behind <c>[Authorize]</c> and serves the
/// api-tester. It is part of the production build (not environment-gated) but inaccessible
/// without a valid JWT.</para>
/// </summary>
public static class CurriculumModule
{
    public static IServiceCollection AddCurriculumModule(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.AddCurriculumApplication();
        services.AddCurriculumInfrastructure(configuration);

        // Expose the RetrievalController (BE-8 dev-only endpoint) via the ASP.NET MVC part
        // discovery system. Without this the controller is not found by the Host's route table.
        services.AddMvc()
            .AddApplicationPart(typeof(CurriculumModule).Assembly);

        return services;
    }

    /// <summary>
    /// Host-callable startup hook. Applies any pending Curriculum migrations, runs the
    /// idempotent chunk seeder, and ensures the curriculum MinIO bucket exists.
    ///
    /// <para>BL-01 Q1: <see cref="CurriculumBucketEnsureService.EnsureBucketAsync"/> is called here
    /// to provision the dedicated <c>"curriculum"</c> bucket at startup (there is no existing
    /// bucket-ensure routine — <c>avatars</c> is assumed pre-created). Fail-soft: a bucket error
    /// is logged but does not block the Host.</para>
    /// </summary>
    public static async Task InitializeAsync(IServiceProvider serviceProvider)
    {
        var dbContext = serviceProvider.GetRequiredService<CurriculumDbContext>();
        await dbContext.Database.MigrateAsync();

        await CurriculumChunkSeeder.SeedAsync(serviceProvider);

        // BL-01 Q1: Ensure the "curriculum" MinIO bucket exists at startup.
        // Fail-soft: a bucket-ensure failure is logged but does not crash the Host.
        var bucketEnsure = serviceProvider.GetRequiredService<CurriculumBucketEnsureService>();
        await bucketEnsure.EnsureBucketAsync();
    }
}

/// <summary>Extension to map any Curriculum module minimal-API endpoints (none at P3-07).</summary>
public static class CurriculumModuleEndpoints
{
    public static WebApplication MapCurriculumModule(this WebApplication app) => app;
}
