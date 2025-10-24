using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Dataisland.Configuration;

public static class ConfigurationExtensions
{
    public static IHostApplicationBuilder AddYamlConfiguration(
        this IHostApplicationBuilder builder
    )
    {
        builder.Configuration
            .AddYamlFile("appsettings.yml", optional: false, reloadOnChange: true)
            .AddYamlFile($"appsettings.{builder.Environment.EnvironmentName}.yml", optional: true,
                reloadOnChange: true)
            .AddEnvironmentVariables();
        return builder;
    }
}