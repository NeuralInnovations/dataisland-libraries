using Microsoft.Extensions.Configuration;

namespace Dataisland.MongoDB;

public static class MongoDBOptionsExtensions
{
    public static MongoDBOptions GetMongoDbOptions(this IConfiguration configuration, string section = "MongoDB")
    {
        return configuration.GetSection(section).Get<MongoDBOptions>()!;
    }
}