using Microsoft.Extensions.DependencyInjection;

namespace Dataisland.LLM;

public static class LlmExtensions
{
    public static IServiceCollection AddLlmProviders(this IServiceCollection services, LlmOptions options)
    {
        services.AddSingleton(options);
        services.AddSingleton<ILlmService, LlmService>();
        return services;
    }
}
