using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.MinIO;

public static class MinIOExtensions
{
    public static IServiceCollection AddMinIO(this IServiceCollection services, MinIOOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, MinIOFileStorage>();
        return services;
    }
}
