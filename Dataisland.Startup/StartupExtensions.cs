using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Dataisland.Startup;

public static class StartupExtensions
{
    class HostStartupImpl(IHost application) : IHostStartup
    {
        public IServiceProvider Services { get; } = application.Services;
    }

    public static void Configure<T>(this IHostStartup startup)
    {
        startup.Services.GetRequiredService<StartupChecker<T>>().Configure();
    }
    
    public static void Configure<T>(this IServiceProvider provider)
    {
        provider.GetRequiredService<StartupChecker<T>>().Configure();
    }

    public static void AddStartupChecker<T>(this IServiceCollection services)
    {
        services.AddSingleton<StartupChecker<T>>();
        services.AddSingleton<IStartup>(x => x.GetRequiredService<StartupChecker<T>>());
    }

    public static async Task StartupAndRunAsync(
        this IHost application,
        Func<IHostStartup, Task> startup,
        CancellationToken cancellationToken = default
    )
    {
        await startup(new HostStartupImpl(application));

        foreach (var component in application.Services.GetServices<IStartup>())
        {
            await component.StartupAsync(cancellationToken);
        }

        await application.RunAsync(cancellationToken);
    }
}