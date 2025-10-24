using MongoDB.Driver;

namespace Dataisland.MongoDB;

public abstract class Repository<TDocument>(
    string collectionName,
    IMongoDBProvider provider
) : IRepository
{
    public IMongoDatabase Database { get; } = provider.Database;

    public IMongoCollection<TDocument> Collection { get; } = provider
        .Database
        .GetCollection<TDocument>(collectionName);


    public IMongoCollection<TDocument> Primary { get; } = provider
        .Database.WithReadPreference(ReadPreference.PrimaryPreferred)
        .GetCollection<TDocument>(collectionName);


    public IMongoCollection<TDocument> Secondary { get; } = provider
        .Database.WithReadPreference(ReadPreference.SecondaryPreferred)
        .GetCollection<TDocument>(collectionName);
}