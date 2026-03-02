using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.ObjectStorage;

public static class ObjectStorageExtensions
{
    public static IServiceCollection AddObjectStorage(this IServiceCollection services, ObjectStorageOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<IFileStorage, S3FileStorage>();
        return services;
    }
}
