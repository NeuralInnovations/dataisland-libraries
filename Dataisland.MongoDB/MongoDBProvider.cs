using MongoDB.Driver;

namespace Dataisland.MongoDB;

public class MongoDBProvider(
    MongoUrl url,
    MongoClientSettings settings
) : IMongoDBProvider, IMongoDBConnection
{
    public IMongoClient Client { get; private set; } = null!;
    public IMongoDatabase Database { get; private set; } = null!;

    public Task ConnectAsync()
    {
        Client = new MongoClient(settings);
        Database = Client.GetDatabase(url.DatabaseName);
        return Task.CompletedTask;
    }
}