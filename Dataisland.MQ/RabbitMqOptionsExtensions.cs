using Microsoft.Extensions.Configuration;

namespace Dataisland.MQ;

public static class RabbitMqOptionsExtensions
{
    public static RabbitMqOptions GetRabbitMqOptions(this IConfiguration configuration, string section = "RabbitMq")
    {
        return configuration.GetSection(section).Get<RabbitMqOptions>()!;
    }
}