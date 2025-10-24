using Serilog;

namespace Dataisland.Serilog;

public static class App
{
    public static void RegisterExitOnUnhandledException()
    {
        Log.Logger = new LoggerConfiguration()
            .WriteTo.Console()
            .CreateBootstrapLogger();

        Log.Information("Starting up!");
        
        // Initialize Serilog
        AppDomain.CurrentDomain.UnhandledException += (sender, args) =>
        {
            Log.Logger.Fatal((Exception)args.ExceptionObject,
                "Unhandled exception. Terminating application: {Terminating}",
                args.IsTerminating);
            Log.CloseAndFlush();

            Environment.Exit(1);
        };
    }
}