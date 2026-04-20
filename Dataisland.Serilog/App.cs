using Serilog;
using Serilog.Enrichers.Span;

namespace Dataisland.Serilog;

public static class App
{
    public static void RegisterExitOnUnhandledException()
    {
        Log.Logger = new LoggerConfiguration()
            .Enrich.WithSpan()
            .WriteTo.Console(
                outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj} trace_id={TraceId} span_id={SpanId} {Properties:j}{NewLine}{Exception}")
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