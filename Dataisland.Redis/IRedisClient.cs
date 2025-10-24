using StackExchange.Redis;

namespace Dataisland.Redis;

public interface IRedisClient
{
    ConnectionMultiplexer Connection { get; }
    IDatabase Database { get; }
}