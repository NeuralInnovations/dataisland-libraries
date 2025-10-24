namespace Dataisland.Startup;

public interface IHostStartup
{
    IServiceProvider Services { get; }
}