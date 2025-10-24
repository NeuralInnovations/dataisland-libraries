using MongoDB.Driver;

namespace Dataisland.MongoDB.Migrations;

public abstract class Migration
{
    internal IMongoClient InternalClient;
    internal IMongoDatabase InternalDatabase;

    protected IMongoClient Client => InternalClient;
    protected IMongoDatabase Database => InternalDatabase;

    protected IMongoCollection<TDocument> GetCollection<TDocument>(string name)
        => Database.GetCollection<TDocument>(name);

    public abstract Task UpAsync();
    public abstract Task DownAsync();

    public virtual bool ShouldUp() => true;
    public virtual bool ShouldDown() => true;
}