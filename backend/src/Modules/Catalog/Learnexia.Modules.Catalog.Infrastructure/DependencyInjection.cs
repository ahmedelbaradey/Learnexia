using System.Reflection;
using Learnexia.Modules.Catalog.Application.Abstractions;
using Learnexia.Modules.Catalog.Infrastructure.Persistence;
using Learnexia.Modules.Catalog.Infrastructure.Repository;
using Learnexia.Modules.Catalog.Infrastructure.Service;
using Learnexia.Shared.Kernel.Abstractions;
using Learnexia.Shared.Kernel.Logging;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Pgvector.EntityFrameworkCore;

namespace Learnexia.Modules.Catalog.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddCatalogInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        //Onion Services
        var types = Assembly.GetExecutingAssembly().GetTypes().Where(t => t.FullName.Contains("Onion") && t.IsClass && !t.IsAbstract && IsSubclassOfRawGeneric(t, typeof(BaseService<>)));
        foreach (var implementationType in types)
        {
            var interfaceTypes = implementationType.GetInterfaces().Where(i => !i.Name.StartsWith("IBase") && i.Assembly == typeof(Learnexia.Shared.Kernel.AssemblyReference).Assembly);
            foreach (var interfaceType in interfaceTypes)
            {
                if (!services.Any(s => s.ServiceType == interfaceType))
                {
                    services.AddScoped(interfaceType, implementationType);
                }
            }
        }
        services.AddLoggerServices(configuration);
        services.AddDbContext(configuration);

        services.AddHttpContextAccessor();

        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IGenericRepository, GenericRepository>();
        services.AddScoped<IRepositoryManager, RepositoryManager>();
        services.AddScoped<IServiceManager, ServiceManager>();

        return services;
    }
    public static void AddDbContext(this IServiceCollection services, IConfiguration configuration)
    {
      services.AddDbContext<CatalogDbContext>(options => options.ConfigureWarnings(warnings => warnings.Ignore(RelationalEventId.PendingModelChangesWarning))
      .UseNpgsql(configuration.GetConnectionString("default"),  builder => builder
          .MigrationsHistoryTable("__EFMigrationsHistory", CatalogDbContext.Schema)
          .MigrationsAssembly(typeof(CatalogDbContext).Assembly.FullName)
          .UseVector()));
    }

    public static IServiceCollection AddLoggerServices(this IServiceCollection services, IConfiguration configuration)
    {
            services.AddSingleton<ILoggerManager, LoggerManager>();
            return services;
    }
    private static bool IsSubclassOfRawGeneric(Type type, Type generic)
    {
         while (type != null && type != typeof(object))
         {
             var current = type.IsGenericType ? type.GetGenericTypeDefinition() : type;
             if (current == generic)
                 return true;
             type = type.BaseType;
         }
         return false;
    }
}
