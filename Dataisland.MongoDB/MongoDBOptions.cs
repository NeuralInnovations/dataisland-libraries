namespace Dataisland.MongoDB;

[Serializable]
public class MongoDBOptions
{
    public string ConnectionString { get; set; } = "mongodb://localhost:27017";

    public int MinConnectionPoolSize { get; set; } = 50;

    public int MaxConnectionPoolSize { get; set; } = 500;

    public TimeSpan WaitQueueTimeout { get; set; } = TimeSpan.FromMinutes(2);
}