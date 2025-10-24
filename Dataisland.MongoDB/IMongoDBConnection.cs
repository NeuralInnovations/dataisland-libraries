namespace Dataisland.MongoDB;

public interface IMongoDBConnection
{
    Task ConnectAsync();
}