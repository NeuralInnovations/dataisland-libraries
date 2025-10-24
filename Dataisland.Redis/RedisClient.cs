using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using StackExchange.Redis;

namespace Dataisland.Redis;

public class RedisClient(
    IOptions<RedisOptions> options,
    ILogger<RedisClient> logger
)
    : IRedisClient, IRedisConnection, IDisposable
{
    private bool _disposed;

    public async Task ConnectAsync()
    {
        ConnectionMultiplexer.SetFeatureFlag("PreventThreadTheft", true);
        var configuration = ConfigurationOptions.Parse(options.Value.ConnectionString);
        var connection = await ConnectionMultiplexer.ConnectAsync(configuration);
        Connection = connection;
        Database = connection.GetDatabase();

        RegisterLogger(connection, logger);
    }

    public ConnectionMultiplexer Connection { get; private set; } = null!;
    public IDatabase Database { get; private set; } = null!;

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        Connection.Dispose();
    }

    private void RegisterLogger(ConnectionMultiplexer connection, ILogger<RedisClient> logger)
    {
        connection.ConfigurationChanged += (sender, args) =>
        {
            logger.LogInformation(
                $"{nameof(connection.ConfigurationChanged)} {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.ConnectionFailed += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                $"{nameof(connection.ConnectionFailed)} {nameof(args.ConnectionType)}:{args.ConnectionType}, {nameof(args.FailureType)}:{args.FailureType}, {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.ConnectionRestored += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                $"{nameof(connection.ConnectionRestored)} {nameof(args.ConnectionType)}:{args.ConnectionType}, {nameof(args.FailureType)}:{args.FailureType}, {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.ErrorMessage += (sender, args) =>
        {
            logger.LogError(
                $"{nameof(connection.ErrorMessage)} {nameof(args.Message)}:{args.Message}, {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.InternalError += (sender, args) =>
        {
            logger.LogError(
                args.Exception,
                $"{nameof(connection.InternalError)} {nameof(args.ConnectionType)}:{args.ConnectionType}, {nameof(args.Origin)}:{args.Origin}, {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.ConfigurationChangedBroadcast += (sender, args) =>
        {
            logger.LogInformation(
                $"{nameof(connection.ConfigurationChangedBroadcast)} {nameof(args.EndPoint)}:{args.EndPoint}"
            );
        };
        connection.HashSlotMoved += (sender, args) =>
        {
            logger.LogInformation(
                $"{nameof(connection.HashSlotMoved)} {nameof(args.HashSlot)}:{args.HashSlot}, {nameof(args.NewEndPoint)}:{args.NewEndPoint}, {nameof(args.OldEndPoint)}:{args.OldEndPoint}"
            );
        };
        connection.ServerMaintenanceEvent += (sender, args) =>
        {
            logger.LogInformation(
                $"{nameof(connection.ServerMaintenanceEvent)} {nameof(args.RawMessage)}:{args.RawMessage}, {nameof(args.ReceivedTimeUtc)}:{args.ReceivedTimeUtc}, {nameof(args.StartTimeUtc)}:{args.StartTimeUtc}"
            );
        };
    }
}