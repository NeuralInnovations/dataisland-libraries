using Microsoft.Extensions.Configuration;

namespace Dataisland.MQ;

public static class RabbitMqOptionsExtensions
{
    public static RabbitMqOptions GetRabbitMqOptions(this IConfiguration configuration, string section = "RabbitMq")
    {
        var options = configuration.GetSection(section).Get<RabbitMqOptions>()!;
        options.ApplyConnectionString();
        return options;
    }
}
