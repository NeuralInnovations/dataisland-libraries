using MongoDB.Driver;

namespace Dataisland.MongoDB;

public interface IMongoDBProvider
{
    IMongoClient Client { get; }
    IMongoDatabase Database { get; }
}