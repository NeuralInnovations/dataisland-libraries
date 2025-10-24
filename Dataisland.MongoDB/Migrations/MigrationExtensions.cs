using Dataisland.Startup;
using Microsoft.Extensions.DependencyInjection;
using System.Linq; // added for reflection LINQ operations

namespace Dataisland.MongoDB.Migrations;

public static class MigrationExtensions
{
    public static IServiceCollection AddMigrations<TMigration>(this IServiceCollection services)
        where TMigration : Migration
    {
        // Register both as concrete and as base Migration so it is discoverable via IEnumerable<Migration>
        services.AddSingleton<TMigration>();
        services.AddSingleton<Migration, TMigration>();
        services.AddSingleton<IMigrationRunner, MigrationRunner>();
        return services;
    }

    public static IServiceCollection AddMigrationsInAssemblyOfType<TMigration>(this IServiceCollection services)
        where TMigration : Migration
    {
        // Discover all concrete Migration implementations in the assembly of the provided type
        var migrationBaseType = typeof(Migration);
        var types = new[] { typeof(TMigration).Assembly }
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsInterface: false }
                && migrationBaseType.IsAssignableFrom(type)
            );

        foreach (var impl in types)
        {
            services.AddSingleton(typeof(Migration), impl);
            services.AddSingleton(impl); // also register concrete for convenience if requested directly
        }

        services.AddSingleton<IMigrationRunner, MigrationRunner>();

        return services;
    }

    public static Task RunMongoDBMigrationsAsync(this IHostStartup hostStartup, MigrationVersion version)
    {
        var runner = hostStartup.Services.GetRequiredService<IMigrationRunner>();
        return runner.RunAsync(version, CancellationToken.None);
    }
}