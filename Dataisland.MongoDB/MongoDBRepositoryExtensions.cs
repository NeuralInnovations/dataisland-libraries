using System.Diagnostics.Contracts;
using Dataisland.Cache;
using Dataisland.MongoDB.Cache;
using Dataisland.Startup;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Dataisland.MongoDB;

public static class MongoDBRepositoryExtensions
{
    public static Task RunMongoDBAsync(this IHostStartup hostStartup)
    {
        return hostStartup.Services.RunMongoDBAsync();
    }

    public static async Task RunMongoDBAsync(this IServiceProvider provider)
    {
        var logger = provider.GetRequiredService<ILogger<IMongoDBConnection>>();

        logger.LogInformation($"[{nameof(IMongoDBConnection)}]⏳ Connecting to MongoDB ...");
        // Connect to MongoDB
        await Task.WhenAll(
            tasks: provider
                .GetServices<IMongoDBConnection>()
                .Select(x => x.ConnectAsync())
        );
        logger.LogInformation($"[{nameof(IMongoDBConnection)}]✅ Connected to MongoDB");

        logger.LogInformation($"[{nameof(IRepositoryApplyIndex)}]⏳ Applying indexes ...");
        // Apply indexes
        await Task.WhenAll(
            tasks: provider
                .GetServices<IRepositoryApplyIndex>()
                .Select(x =>
                {
                    var loggerType = typeof(ILogger<>).MakeGenericType(x.GetType());
                    var xLogger = (ILogger)provider.GetRequiredService(loggerType);
                    return x.ApplyAsync(xLogger);
                })
        );
        logger.LogInformation($"[{nameof(IRepositoryApplyIndex)}]✅ Applied indexes");

        provider.Configure<IMongoDBProvider>();
    }

    public static IServiceCollection AddMongoDB(
        this IServiceCollection services,
        MongoDBOptions options,
        Func<MongoClientSettings, (IMongoDBProvider, IMongoDBConnection)>? factory = null
    )
    {
        var url = new MongoUrl(options.ConnectionString);

        var settings = MongoClientSettings.FromUrl(url);

        settings.MinConnectionPoolSize = options.MinConnectionPoolSize;
        settings.MaxConnectionPoolSize = options.MaxConnectionPoolSize;

        settings.WaitQueueTimeout = options.WaitQueueTimeout;

        // factory
        (IMongoDBProvider Provider, IMongoDBConnection Connection) client;
        if (factory != null)
        {
            client = factory(settings);
        }
        else
        {
            var instance = new MongoDBProvider(url, settings);
            client = (instance, instance);
        }

        services.AddStartupChecker<IMongoDBProvider>();
        services.AddSingleton<IMongoDBProvider>(_ => client.Provider);
        services.AddSingleton(client.GetType(), _ => client);
        services.AddSingleton<IMongoDBConnection>(x => client.Connection);

        return services;
    }

    public static IServiceCollection AddMongoDBCache(this IServiceCollection services)
    {
        services.AddSingleton(typeof(ICache<>), typeof(MongoCache<>));
        return services;
    }

    public static IMongoClient GetMongoClient(this IServiceProvider provider)
    {
        var mongo = provider.GetRequiredService<IMongoDBProvider>();
        return mongo.Client;
    }

    public static IMongoDBProvider GetMongoProvider(this IServiceProvider provider)
    {
        var mongo = provider.GetRequiredService<IMongoDBProvider>();
        return mongo;
    }

    public static IServiceCollection AddRepository<T>(this IServiceCollection services)
        where T : class, IRepository
    {
        Contract.Assert(!typeof(T).IsInterface, $"{typeof(T)} must NOT be an interface");
        Contract.Assert(!typeof(T).IsAbstract, $"{typeof(T)} must NOT be an abstract class");

        var api = typeof(T).GetInterfaces();

        services.AddSingleton<T>();
        foreach (var type in api)
        {
            services.AddSingleton(type, sp => sp.GetRequiredService<T>());
        }

        return services;
    }

    public static IServiceCollection AddRepository<TApi, TImpl>(this IServiceCollection services)
        where TApi : class
        where TImpl : class, IRepository, TApi
    {
        Contract.Assert(!typeof(TImpl).IsInterface, $"{typeof(TImpl)} must NOT be an interface");
        Contract.Assert(!typeof(TImpl).IsAbstract, $"{typeof(TImpl)} must NOT be an abstract class");

        services.AddSingleton<TImpl>();
        services.AddSingleton<TApi>(x => x.GetRequiredService<TImpl>());
        services.AddSingleton<IRepository>(x => x.GetRequiredService<TImpl>());

        if (typeof(IRepositoryApplyIndex).IsAssignableFrom(typeof(TImpl)))
        {
            services.AddSingleton<IRepositoryApplyIndex>(
                x => (IRepositoryApplyIndex)x.GetRequiredService<TImpl>()
            );
        }

        return services;
    }

    public static IServiceCollection AddRepositories(this IServiceCollection services)
    {
        var repositoryBaseType = typeof(IRepository);
        var types = AppDomain.CurrentDomain.GetAssemblies()
            .SelectMany(assembly => assembly.GetTypes())
            .Where(type =>
                type is { IsClass: true, IsAbstract: false, IsInterface: false }
                && repositoryBaseType.IsAssignableFrom(type)
            );

        foreach (var impl in types)
        {
            var api = impl.GetInterfaces();
            if (api.Any())
            {
                services.AddSingleton(impl);
                foreach (var type in api)
                {
                    services.AddSingleton(type, sp => sp.GetRequiredService(impl));
                }
            }
            else
            {
                services.AddSingleton(impl);
            }
        }

        return services;
    }

    public static IHealthChecksBuilder AddMongoDbHealthCheck(
        this IHealthChecksBuilder builder
    )
    {
        builder.AddMongoDb(clientFactory: sp => sp.GetRequiredService<IMongoDBProvider>().Client);
        return builder;
    }


    public static FilterDefinitionBuilder<T> Filter<T>(this Repository<T> repository) => Builders<T>.Filter;

    public static IndexKeysDefinitionBuilder<T> Index<T>(this Repository<T> repository) => Builders<T>.IndexKeys;

    public static ProjectionDefinitionBuilder<T> Projection<T>(this Repository<T> repository) => Builders<T>.Projection;

    public static UpdateDefinitionBuilder<T> Update<T>(this Repository<T> repository) => Builders<T>.Update;

    public static SortDefinitionBuilder<T> Sort<T>(this Repository<T> repository) => Builders<T>.Sort;
}