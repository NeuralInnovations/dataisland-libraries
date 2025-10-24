namespace Dataisland.Redis;

[Serializable]
public class RedisOptions
{
    public string ConnectionString { get; set; } = "localhost:6379,resolveDns=true";
}