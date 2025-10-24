namespace Dataisland.Startup;

public interface IStartup
{
    Task StartupAsync(CancellationToken cancellationToken = default);
}

public class StartupChecker<T>(string? message = null) : IStartup
{
    public string Message { get; } =
        message ?? $"{typeof(T)} is not configured, run it on {nameof(StartupExtensions.StartupAndRunAsync)}";

    public Task StartupAsync(CancellationToken cancellationToken = default)
    {
        if (!Configured)
        {
            throw new InvalidOperationException(
                Message
            );
        }

        return Task.CompletedTask;
    }

    public void Configure() => Configured = true;

    private bool Configured { get; set; }
}