namespace Dataisland.Redis;

public interface IRedisConnection
{
    Task ConnectAsync();
}